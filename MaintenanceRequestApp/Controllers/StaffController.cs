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
    [Authorize(Roles = "NhanVienKyThuat")]
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

        public async Task<IActionResult> Index()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var assignments = await _context.RequestAssignments
                .Include(a => a.RequestMaintenance)
                .Where(a => a.UserId == userId)
                .Select(a => a.RequestMaintenance)
                .Distinct()
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(assignments);
        }

        [HttpPost]
        public async Task<IActionResult> AddReport(Guid requestId, string noteContent, IFormFile imageFile, bool notifyManager)
        {
            var req = await _context.RequestMaintenances.FindAsync(requestId);
            if (req != null && !string.IsNullOrEmpty(noteContent))
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var note = new MaintenanceNote
                {
                    RequestId = requestId,
                    UserId = userId,
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
                    Action = "Nhân viên gửi báo cáo",
                    Timestamp = DateTime.UtcNow
                });
                
                if (req.Status < 3) req.Status = 3;
                if (!req.StartTime.HasValue) req.StartTime = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                if (notifyManager)
                {
                    var managers = await _context.Users.Where(u => u.Role == "QuanLyKyThuat" || u.Role == "Admin").ToListAsync();
                    foreach (var mgr in managers)
                    {
                        if (!string.IsNullOrEmpty(mgr.Email))
                        {
                            await _emailService.SendEmailAsync(mgr.Email, "Báo cáo mới từ Nhân viên Kỹ thuật", 
                                $"Nhân viên {User.Identity.Name} vừa báo cáo tiến độ sự cố {req.EquipmentDamged}. Nội dung: {noteContent}");
                        }
                    }
                }
                
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Nhân viên {User.Identity.Name} vừa cập nhật báo cáo cho yêu cầu {requestId}.");
            }
            return RedirectToAction("Index");
        }
    }
}
