using Microsoft.AspNetCore.Mvc;
using MaintenanceRequestApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
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
        private readonly ILogger<ManagerController> _logger;
        private readonly IConfiguration _configuration;

        public ManagerController(MaintenanceDbContext context, IImageProcessingService imageService, IWebHostEnvironment env, IEmailService emailService, IHubContext<NotificationHub> hubContext, ILogger<ManagerController> logger, IConfiguration configuration)
        {
            _context = context;
            _imageService = imageService;
            _env = env;
            _emailService = emailService;
            _hubContext = hubContext;
            _logger = logger;
            _configuration = configuration;
        }

        // ─────────────────────────────────────────────
        // INDEX
        // ─────────────────────────────────────────────
        public async Task<IActionResult> Index(int? status, string searchTerm, string staffId, int? pageNumber)
        {
            var query = _context.RequestMaintenances
                .Include(r => r.Assignments)
                .ThenInclude(a => a.User)
                .Where(r => r.Status >= 2)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

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
                query = query.Where(r => r.Assignments!.Any(a => a.UserId == staffId));

            query = query.OrderByDescending(r => r.CreatedAt);

            int pageSize = 10;
            var requests = await MaintenanceRequestApp.Helpers.PaginatedList<RequestMaintenance>.CreateAsync(query, pageNumber ?? 1, pageSize);

            ViewBag.Staffs = await _context.Users.Where(u => u.Role == "NhanVienKyThuat" || u.Role == "QuanLyKyThuat").ToListAsync();

            var viewModel = new MaintenanceRequestApp.ViewModels.MaintenanceListViewModel
            {
                Requests = requests,
                Status = status,
                SearchTerm = searchTerm,
                StaffId = staffId
            };

            return View(viewModel);
        }

        // ─────────────────────────────────────────────
        // DETAILS (GET) — Workspace cho Manager/Admin
        // ─────────────────────────────────────────────
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
                request.AuditLogs = request.AuditLogs.OrderByDescending(l => l.Timestamp).ToList();

            // Danh sách nhân viên cho Select2 (Assign panel)
            ViewBag.Staffs = await _context.Users
                .Where(u => u.Role == "NhanVienKyThuat" || u.Role == "QuanLyKyThuat")
                .ToListAsync();

            return View(request);
        }

        // ─────────────────────────────────────────────
        // APPROVE (POST) — Chỉ Admin
        // ─────────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(Guid id)
        {
            var req = await _context.RequestMaintenances.FindAsync(id);
            if (req != null && req.Status == 1)
            {
                req.Status = 2;
                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = id,
                    UserId = User.Identity!.Name,
                    Action = "Chuyển trạng thái thành Đã Phê duyệt",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Yêu cầu {id} đã được phê duyệt.");
                TempData["SuccessMessage"] = "✅ Yêu cầu đã được phê duyệt thành công!";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // ─────────────────────────────────────────────
        // ASSIGN (POST) — Manager hoặc Admin
        // ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Assign(Guid requestId, List<string> selectedStaffIds, bool notifyEmail)
        {
            var req = await _context.RequestMaintenances.FindAsync(requestId);

            if (req != null && selectedStaffIds != null && selectedStaffIds.Any())
            {
                // 1. Xóa điều phối cũ
                var existing = _context.RequestAssignments.Where(a => a.RequestId == requestId);
                _context.RequestAssignments.RemoveRange(existing);

                var staffNames = new List<string>();

                // 2. Thêm điều phối mới
                foreach (var staffId in selectedStaffIds)
                {
                    var user = await _context.Users.FindAsync(staffId);
                    if (user != null)
                    {
                        _context.RequestAssignments.Add(new RequestAssignment { RequestId = requestId, UserId = staffId });
                        staffNames.Add($"{user.FirstName} {user.LastName}");

                        if (notifyEmail && !string.IsNullOrEmpty(user.Email))
                        {
                            string link = Url.Action("Index", "Staff", null, Request.Scheme)!;
                            await _emailService.SendEmailAsync(user.Email, "Bạn được phân công nhiệm vụ mới",
                                $"Hệ thống vừa phân công cho bạn xử lý sự cố <b>{req.EquipmentDamged}</b>. Vui lòng truy cập <a href='{link}'>đây</a> để xem chi tiết.");
                        }
                        await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Đã điều phối công việc cho {user.Username}.");
                    }
                }

                // 3. Cập nhật trạng thái
                if (req.Status < 3) req.Status = 3;
                if (!req.StartTime.HasValue) req.StartTime = DateTime.UtcNow;

                // 4. Audit Log
                string namesStr = string.Join(", ", staffNames);
                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = requestId,
                    UserId = User.Identity!.Name,
                    Action = $"Đã điều phối cho các nhân viên: {namesStr}",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"✅ Đã điều phối cho: {namesStr}";
            }
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // ─────────────────────────────────────────────
        // ADD NOTE / PROGRESS REPORT (POST)
        // ─────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> AddNote(Guid requestId, string noteContent, IFormFile imageFile, bool sendEmail = false)
        {
            var req = await _context.RequestMaintenances
                .Include(r => r.Assignments).ThenInclude(a => a.User)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (req != null && !string.IsNullOrEmpty(noteContent))
            {
                var note = new MaintenanceNote
                {
                    RequestId = requestId,
                    UserId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    NoteContent = noteContent,
                    ImagePath = string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                if (imageFile != null && imageFile.Length > 0)
                {
                    var baseUploadPath = _configuration["FileUploadSettings:PhysicalPath"] ?? Path.Combine(_env.WebRootPath, "uploads");
                    if (!Path.IsPathRooted(baseUploadPath)) baseUploadPath = Path.Combine(_env.ContentRootPath, baseUploadPath);

                    var uploadFolder = Path.Combine(baseUploadPath, "notes");
                    Directory.CreateDirectory(uploadFolder);
                    var fileName = await _imageService.ProcessAndSaveImageAsync(imageFile, uploadFolder);
                    note.ImagePath = $"/uploads/notes/{fileName}";
                }

                _context.MaintenanceNotes.Add(note);
                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = requestId,
                    UserId = User.Identity!.Name,
                    Action = "Quản lý thêm ghi chú chỉ đạo",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                // Gửi email thông báo cho nhân viên phụ trách
                if (sendEmail && req.Assignments != null)
                {
                    string detailLink = Url.Action("Details", "Staff", new { id = requestId }, Request.Scheme)!;
                    foreach (var assignment in req.Assignments)
                    {
                        if (assignment.User != null && !string.IsNullOrEmpty(assignment.User.Email))
                        {
                            await _emailService.SendEmailAsync(
                                assignment.User.Email,
                                $"[Chỉ đạo mới] Phiếu: {req.EquipmentDamged}",
                                $"Quản lý <b>{User.Identity!.Name}</b> vừa gửi chỉ đạo cho phiếu <b>{req.EquipmentDamged}</b>.<br/><br/>" +
                                $"Nội dung: {noteContent}<br/><br/>" +
                                $"Vui lòng <a href='{detailLink}'>nhấn vào đây</a> để xem chi tiết."
                            );
                        }
                    }
                }

                TempData["SuccessMessage"] = "✅ Đã lưu ghi chú thành công!";
            }
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // ─────────────────────────────────────────────
        // CLOSE / COMPLETE TASK (POST)
        // ─────────────────────────────────────────────
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
                    UserId = User.Identity!.Name,
                    Action = "Xác nhận Hoàn thành nhiệm vụ",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Yêu cầu {id} đã hoàn thành.");
                TempData["SuccessMessage"] = "✅ Nhiệm vụ đã được đánh dấu Hoàn thành!";
            }
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
