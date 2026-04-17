using Microsoft.AspNetCore.Mvc;
using MaintenanceRequestApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using MaintenanceRequestApp.Models;
using MaintenanceRequestApp.Services;
using Microsoft.AspNetCore.Http;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using MaintenanceRequestApp.Hubs;

namespace MaintenanceRequestApp.Controllers
{
    [Authorize(Roles = "QuanLyKyThuat,Admin")]
    public class ManagerController : Controller
    {
        private readonly MaintenanceDbContext _context;
        private readonly IImageProcessingService _imageService;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public ManagerController(MaintenanceDbContext context, IImageProcessingService imageService, IWebHostEnvironment env, IEmailService emailService, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _imageService = imageService;
            _env = env;
            _emailService = emailService;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index(int? status, string searchTerm, string staffId, int page = 1)
        {
            var query = _context.RequestMaintenances
                .Include(r => r.Assignments)
                .ThenInclude(a => a.User)
                .Where(r => r.Status >= 2)
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
        public async Task<IActionResult> Assign(Guid requestId, string userId, bool notifyEmail)
        {
            var req = await _context.RequestMaintenances.FindAsync(requestId);
            var user = await _context.Users.FindAsync(userId);

            if (req != null && user != null)
            {
                var assign = new RequestAssignment { RequestId = requestId, UserId = userId };
                _context.RequestAssignments.Add(assign);
                
                if (req.Status < 3) req.Status = 3;
                if (!req.StartTime.HasValue) req.StartTime = DateTime.UtcNow;

                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = requestId,
                    UserId = User.Identity.Name,
                    Action = $"Quản lý đã điều phối cho: {user.FirstName} {user.LastName}",
                    Timestamp = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();

                if (notifyEmail && !string.IsNullOrEmpty(user.Email))
                {
                    string link = Url.Action("Index", "Staff", null, Request.Scheme);
                    await _emailService.SendEmailAsync(user.Email, "Bạn được phân công nhiệm vụ mới", 
                        $"Hệ thống vừa phân công cho bạn xử lý sự cố {req.EquipmentDamged}. Vui lòng truy cập <a href='{link}'>đây</a> để xem chi tiết.");
                }

                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Đã điều phối công việc cho {user.Username}.");
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> AddNote(Guid requestId, string noteContent, IFormFile imageFile)
        {
            var req = await _context.RequestMaintenances.FindAsync(requestId);
            if (req != null && !string.IsNullOrEmpty(noteContent))
            {
                var note = new MaintenanceNote
                {
                    RequestId = requestId,
                    UserId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    NoteContent = noteContent,
                    CreatedAt = DateTime.UtcNow
                };

                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "notes");
                    var fileName = await _imageService.ProcessAndSaveImageAsync(imageFile, uploadFolder);
                    note.ImagePath = $"/uploads/notes/{fileName}";
                }

                _context.MaintenanceNotes.Add(note);
                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = requestId,
                    UserId = User.Identity.Name,
                    Action = "Quản lý thêm ghi chú chỉ đạo",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Close(Guid id)
        {
            var req = await _context.RequestMaintenances.FindAsync(id);
            if (req != null && req.Status != 4 && req.Status != 5)
            {
                req.Status = 4;
                req.EndTime = DateTime.UtcNow;

                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = id,
                    UserId = User.Identity.Name,
                    Action = "Quản lý duyệt hoàn thành nhiệm vụ",
                    Timestamp = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Yêu cầu {id} đã được đóng.");
            }
            return RedirectToAction("Index");
        }
    }
}
