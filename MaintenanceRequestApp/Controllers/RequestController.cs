using Microsoft.AspNetCore.Mvc;
using MaintenanceRequestApp.Data;
using MaintenanceRequestApp.Models;
using MaintenanceRequestApp.Services;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.EntityFrameworkCore;
using MaintenanceRequestApp.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MaintenanceRequestApp.Controllers
{
    public class RequestController : Controller
    {
        private readonly MaintenanceDbContext _context;
        private readonly IImageProcessingService _imageService;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConfiguration _configuration;

        public RequestController(MaintenanceDbContext context, IImageProcessingService imageService, IWebHostEnvironment env, IHubContext<NotificationHub> hubContext, IConfiguration configuration)
        {
            _context = context;
            _imageService = imageService;
            _env = env;
            _hubContext = hubContext;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RequestMaintenance model, List<IFormFile> uploadedImages)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    model.Id = Guid.NewGuid();
                    model.CreatedAt = DateTime.UtcNow;
                    model.Status = 1; // Khởi tạo
                    
                    // Xử lý giá trị null cho DB constraint
                    if (string.IsNullOrEmpty(model.Urgency)) 
                    {
                        model.Urgency = "Không xác định / Not specified";
                    }

                    // Process images
                    if (uploadedImages != null && uploadedImages.Count > 0)
                    {
                        if (uploadedImages.Count > 3)
                        {
                            ModelState.AddModelError("", "Bạn chỉ được phép tải lên tối đa 3 hình/You can upload a maximum of 3 images.");
                            return View(model);
                        }

                        var baseUploadPath = _configuration["FileUploadSettings:PhysicalPath"] ?? Path.Combine(_env.WebRootPath, "uploads");
                        if (!Path.IsPathRooted(baseUploadPath)) baseUploadPath = Path.Combine(_env.ContentRootPath, baseUploadPath);

                        var uploadFolder = Path.Combine(baseUploadPath, "requests");
                        var savedFiles = await _imageService.ProcessAndSaveMultipleImagesAsync(uploadedImages, uploadFolder);

                        model.Medias = new List<RequestMedia>();
                        foreach (var file in savedFiles)
                        {
                            model.Medias.Add(new RequestMedia
                            {
                                RequestId = model.Id,
                                FilePath = $"/uploads/requests/{file}",
                                UploadedAt = DateTime.UtcNow
                            });
                        }
                    }

                    _context.RequestMaintenances.Add(model);
                    
                    // Audit log
                    _context.AuditLogs.Add(new AuditLog
                    {
                        RequestId = model.Id,
                        UserId = "Khách (System)", // Mặc định do khách ngoài gửi vào
                        Action = "Khởi tạo yêu cầu",
                        Timestamp = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();

                    // Gửi SignalR thông báo có request mới cho Quản trị viên/Quản lý
                    await _hubContext.Clients.All.SendAsync("ReceiveMessage", "Hệ thống", $"Có yêu cầu bảo trì mới từ {model.FullName}.");

                    return RedirectToAction("Success", new { id = model.Id });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Success(Guid id)
        {
            var req = await _context.RequestMaintenances.FindAsync(id);
            if (req == null) return NotFound();
            return View(req);
        }

        [HttpGet]
        public async Task<IActionResult> Status(Guid id)
        {
            var request = await _context.RequestMaintenances
                .Include(r => r.Medias)
                .Include(r => r.AuditLogs)
                .Include(r => r.MaintenanceNotes)
                .Include(r => r.Assignments!)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        [HttpGet]
        public IActionResult Lookup()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Lookup(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId) || !Guid.TryParse(requestId, out Guid parsedGuid))
            {
                ModelState.AddModelError("", "Mã yêu cầu không hợp lệ hoặc không tồn tại / Invalid or non-existent request ID.");
                return View();
            }

            var request = await _context.RequestMaintenances.FindAsync(parsedGuid);
            if (request == null)
            {
                ModelState.AddModelError("", "Không tìm thấy yêu cầu với mã này / Request not found.");
                return View();
            }

            return RedirectToAction("Status", new { id = parsedGuid });
        }
    }
}
