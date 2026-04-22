using Microsoft.AspNetCore.Mvc;
using MaintenanceRequestApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.IO;
using MaintenanceRequestApp.Models;
using MaintenanceRequestApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using MaintenanceRequestApp.Hubs;

namespace MaintenanceRequestApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly MaintenanceDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IEmailService _emailService;
        private readonly IImageProcessingService _imageService;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public AdminController(MaintenanceDbContext context, IHubContext<NotificationHub> hubContext, IEmailService emailService, IImageProcessingService imageService, IWebHostEnvironment env, IConfiguration configuration)
        {
            _context = context;
            _hubContext = hubContext;
            _emailService = emailService;
            _imageService = imageService;
            _env = env;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(int? status, string searchTerm, string staffId, int? pageNumber)
        {
            var query = _context.RequestMaintenances
                .Include(r => r.Assignments)
                .ThenInclude(a => a.User)
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
        // DETAILS (GET) — Interactive Workspace
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

            if (request.AuditLogs != null)
                request.AuditLogs = request.AuditLogs.OrderByDescending(l => l.Timestamp).ToList();

            ViewBag.Staffs = await _context.Users
                .Where(u => u.Role == "NhanVienKyThuat" || u.Role == "QuanLyKyThuat")
                .ToListAsync();

            return View("~/Views/Manager/Details.cshtml", request);
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
                TempData["SuccessMessage"] = "✅ Yêu cầu đã được phê duyệt thành công!";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Assign(Guid requestId, List<string> selectedStaffIds, bool notifyEmail)
        {
            var req = await _context.RequestMaintenances.FindAsync(requestId);

            if (req != null && selectedStaffIds != null && selectedStaffIds.Any())
            {
                // 1. Remove existing RequestAssignment records
                var existingAssignments = _context.RequestAssignments.Where(a => a.RequestId == requestId);
                _context.RequestAssignments.RemoveRange(existingAssignments);
                
                var staffNames = new System.Collections.Generic.List<string>();

                // 2. Iterate and insert new records
                foreach (var staffId in selectedStaffIds)
                {
                    var user = await _context.Users.FindAsync(staffId);
                    if (user != null)
                    {
                        var assign = new RequestAssignment { RequestId = requestId, UserId = staffId };
                        _context.RequestAssignments.Add(assign);
                        staffNames.Add($"{user.FirstName} {user.LastName}");

                        if (notifyEmail && !string.IsNullOrEmpty(user.Email))
                        {
                            string link = Url.Action("Index", "Staff", null, Request.Scheme);
                            await _emailService.SendEmailAsync(user.Email, "Bạn được phân công nhiệm vụ mới", 
                                $"Hệ thống vừa phân công cho bạn xử lý sự cố {req.EquipmentDamged}. Vui lòng truy cập <a href='{link}'>đây</a> để xem chi tiết.");
                        }

                        await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Đã điều phối công việc cho {user.Username}.");
                    }
                }
                
                // 3. Update Status and StartTime
                if (req.Status < 3) req.Status = 3; // Đang sửa chữa
                if (!req.StartTime.HasValue) req.StartTime = DateTime.UtcNow;

                // 4. Audit Log
                string namesStr = string.Join(", ", staffNames);
                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = requestId,
                    UserId = User.Identity.Name,
                    Action = $"Admin đã điều phối cho các nhân viên: {namesStr}",
                    Timestamp = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"✅ Đã điều phối thành công cho: {namesStr}";
            }
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteTask(Guid id, string? finalNote, IFormFile? imageFile, bool notifyRequester = false)
        {
            var req = await _context.RequestMaintenances.FindAsync(id);
            if (req != null && req.Status != 4 && req.Status != 5)
            {
                req.Status = 4; // Hoàn thành
                req.EndTime = DateTime.UtcNow;

                _context.AuditLogs.Add(new AuditLog
                {
                    RequestId = id,
                    UserId = User.Identity!.Name,
                    Action = "Chuyển trạng thái thành Hoàn thành",
                    Timestamp = DateTime.UtcNow
                });
                
                if (!string.IsNullOrEmpty(finalNote) || (imageFile != null && imageFile.Length > 0))
                {
                    var note = new MaintenanceNote
                    {
                        RequestId = id,
                        UserId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                        NoteContent = string.IsNullOrEmpty(finalNote) ? "Đã nghiệm thu hoàn thành." : finalNote,
                        IsPublicResponse = true,
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
                }

                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"Yêu cầu {id} đã hoàn thành.");
                TempData["SuccessMessage"] = "✅ Nhiệm vụ đã được đánh dấu Hoàn thành!";

                if (notifyRequester && !string.IsNullOrEmpty(req.Email))
                {
                    var statusLink = Url.Action("Status", "Request", new { id = req.Id }, Request.Scheme);
                    var emailBody = $"Chào {req.FullName}, Yêu cầu sửa chữa {req.EquipmentDamged} tại {req.Location} của bạn đã hoàn tất. Bạn có thể xem kết quả chi tiết tại đây: {statusLink}";
                    await _emailService.SendEmailAsync(req.Email, $"[VIAA Maintenance] Hoàn thành yêu cầu sửa chữa: {req.EquipmentDamged}", emailBody);
                }
            }
            return RedirectToAction(nameof(Details), new { id });
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
                    Action = "Admin thêm ghi chú",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "✅ Đã lưu ghi chú thành công!";
            }
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        [HttpGet]
        public async Task<IActionResult> Users(string searchTerm, int? pageNumber)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                query = query.Where(u => 
                    (u.Username != null && u.Username.ToLower().Contains(term)) ||
                    (u.FirstName != null && u.FirstName.ToLower().Contains(term)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(term))
                );
            }

            query = query.OrderBy(u => u.Username);

            int pageSize = 15;
            var users = await MaintenanceRequestApp.Helpers.PaginatedList<User>.CreateAsync(query, pageNumber ?? 1, pageSize);

            var viewModel = new MaintenanceRequestApp.ViewModels.UserListViewModel
            {
                Users = users,
                SearchTerm = searchTerm
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeRole(string userId, string newRole)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.Role = newRole;
                await _context.SaveChangesAsync();
                TempData["ToastMessage"] = "Cập nhật quyền thành công!";
            }
            return RedirectToAction("Users");
        }
    }
}
