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
    [Authorize(Roles = "NhanVienKyThuat,QuanLyKyThuat")]
    public class StaffController : Controller
    {
        private readonly MaintenanceDbContext _context;
        private readonly IImageProcessingService _imageService;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly IHubContext<NotificationHub> _hubContext;

        public StaffController(MaintenanceDbContext context, IImageProcessingService imageService, IWebHostEnvironment env, IEmailService emailService, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _imageService = imageService;
            _env = env;
            _emailService = emailService;
            _hubContext = hubContext;
        }

        public async Task<IActionResult> Index(int? status, string searchTerm, int? pageNumber)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            var query = _context.RequestAssignments
                .Include(a => a.RequestMaintenance)
                .Where(a => a.UserId == userId)
                .Select(a => a.RequestMaintenance)
                .Distinct()
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(r => 
                    (r.EquipmentDamged != null && r.EquipmentDamged.ToLower().Contains(term)) || 
                    (r.Description != null && r.Description.ToLower().Contains(term)) ||
                    (r.Location != null && r.Location.ToLower().Contains(term))
                );
            }

            query = query.OrderByDescending(r => r.CreatedAt);

            int pageSize = 10;
            var paginatedRequests = await MaintenanceRequestApp.Helpers.PaginatedList<RequestMaintenance>.CreateAsync(query, pageNumber ?? 1, pageSize);

            var viewModel = new MaintenanceRequestApp.ViewModels.MaintenanceListViewModel
            {
                Requests = paginatedRequests,
                Status = status,
                SearchTerm = searchTerm
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var request = await _context.RequestMaintenances
                .Include(r => r.Medias)
                .Include(r => r.Assignments)
                    .ThenInclude(a => a.User)
                .Include(r => r.MaintenanceNotes)
                    .ThenInclude(n => n.User)
                .Include(r => r.AuditLogs)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            // Sắp xếp AuditLogs mới nhất lên đầu
            if (request.AuditLogs != null)
            {
                request.AuditLogs = request.AuditLogs.OrderByDescending(l => l.Timestamp).ToList();
            }

            return View(request);
        }

        [HttpPost]
        public async Task<IActionResult> AddReport(Guid requestId, string noteContent, IFormFile imageFile, bool sendEmail = false)
        {
            var req = await _context.RequestMaintenances.FindAsync(requestId);
            if (req != null && !string.IsNullOrEmpty(noteContent))
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var staffName = User.Identity!.Name ?? "Nhân viên";

                var note = new MaintenanceNote
                {
                    RequestId = requestId,
                    UserId = userId,
                    NoteContent = noteContent,
                    ImagePath = string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "notes");
                    Directory.CreateDirectory(uploadFolder);
                    var fileName = await _imageService.ProcessAndSaveImageAsync(imageFile, uploadFolder);
                    note.ImagePath = $"/uploads/notes/{fileName}";
                }

                _context.MaintenanceNotes.Add(note);
                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = requestId,
                    UserId = staffName,
                    Action = "Nhân viên gửi báo cáo tiến độ",
                    Timestamp = DateTime.UtcNow
                });

                if (req.Status < 3) req.Status = 3;
                if (!req.StartTime.HasValue) req.StartTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Gửi email thông báo cho Quản lý
                if (sendEmail)
                {
                    var managers = await _context.Users
                        .Where(u => u.Role == "QuanLyKyThuat" || u.Role == "Admin")
                        .ToListAsync();

                    string detailLink = Url.Action("Details", "Manager", new { id = requestId }, Request.Scheme)!;
                    foreach (var mgr in managers)
                    {
                        if (!string.IsNullOrEmpty(mgr.Email))
                        {
                            await _emailService.SendEmailAsync(
                                mgr.Email,
                                $"[Báo cáo tiến độ] Phiếu: {req.EquipmentDamged}",
                                $"Nhân viên <b>{staffName}</b> vừa cập nhật tiến độ sửa chữa cho phiếu <b>{req.EquipmentDamged}</b> tại {req.Location}.<br/><br/>" +
                                $"Nội dung: {noteContent}<br/><br/>" +
                                $"Vui lòng <a href='{detailLink}'>nhấn vào đây</a> để xem chi tiết."
                            );
                        }
                    }
                }

                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System",
                    $"Nhân viên {staffName} vừa cập nhật báo cáo cho yêu cầu {req.EquipmentDamged}.");

                TempData["SuccessMessage"] = "✅ Báo cáo đã được gửi thành công!";
            }
            return RedirectToAction(nameof(Details), new { id = requestId });
        }
    }
}
