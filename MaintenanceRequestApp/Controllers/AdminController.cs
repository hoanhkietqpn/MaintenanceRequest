using Microsoft.AspNetCore.Mvc;
using MaintenanceRequestApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using MaintenanceRequestApp.Models;
using Microsoft.AspNetCore.SignalR;
using MaintenanceRequestApp.Hubs;

namespace MaintenanceRequestApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly MaintenanceDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;

        public AdminController(MaintenanceDbContext context, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index(int? status, string searchTerm, string staffId, int page = 1)
        {
            var query = _context.RequestMaintenances
                .Include(r => r.Assignments)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(r => 
                    (r.FullName != null && r.FullName.ToLower().Contains(term)) ||
                    (r.EquipmentDamged != null && r.EquipmentDamged.ToLower().Contains(term)) ||
                    (r.Description != null && r.Description.ToLower().Contains(term)) ||
                    (r.Location != null && r.Location.ToLower().Contains(term))
                );
            }

            if (!string.IsNullOrEmpty(staffId))
            {
                query = query.Where(r => r.Assignments.Any(a => a.UserId == staffId));
            }

            query = query.OrderByDescending(r => r.CreatedAt);

            int pageSize = 10;
            int totalCount = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var requests = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.Staffs = await _context.Users.Where(u => u.Role == "NhanVienKyThuat" || u.Role == "QuanLyKyThuat").ToListAsync();

            var viewModel = new MaintenanceRequestApp.ViewModels.MaintenanceListViewModel
            {
                Requests = requests,
                Status = status,
                SearchTerm = searchTerm,
                StaffId = staffId,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                TotalCount = totalCount
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(Guid id)
        {
            var req = await _context.RequestMaintenances.FindAsync(id);
            if (req != null && req.Status == 1)
            {
                req.Status = 2; // Đã Phê duyệt
                
                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = id,
                    UserId = User.Identity.Name,
                    Action = "Chuyển trạng thái thành Đã Phê duyệt",
                    Timestamp = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Yêu cầu {id} đã được phê duyệt.");
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Assign(Guid requestId, string userId)
        {
            var req = await _context.RequestMaintenances.FindAsync(requestId);
            if (req != null)
            {
                var assign = new RequestAssignment { RequestId = requestId, UserId = userId };
                _context.RequestAssignments.Add(assign);
                
                if (req.Status < 3) req.Status = 3; // Đang sửa chữa
                if (!req.StartTime.HasValue) req.StartTime = DateTime.UtcNow;

                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = requestId,
                    UserId = User.Identity.Name,
                    Action = $"Đã điều phối cho nhân viên {userId}",
                    Timestamp = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Yêu cầu {requestId} đã được điều phối.");
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Close(Guid id)
        {
            var req = await _context.RequestMaintenances.FindAsync(id);
            if (req != null && req.Status != 4 && req.Status != 5)
            {
                req.Status = 4; // Hoàn thành
                req.EndTime = DateTime.UtcNow;

                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = id,
                    UserId = User.Identity.Name,
                    Action = "Chuyển trạng thái thành Hoàn thành",
                    Timestamp = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Yêu cầu {id} đã hoàn thành.");
            }
            return RedirectToAction("Index");
        }
    }
}
