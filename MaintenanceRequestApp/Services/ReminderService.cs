using System.Linq;
using System.Threading.Tasks;
using MaintenanceRequestApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace MaintenanceRequestApp.Services
{
    public class ReminderService : IReminderService
    {
        private readonly MaintenanceDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReminderService> _logger;
        private readonly IConfiguration _configuration;

        public ReminderService(MaintenanceDbContext context, IEmailService emailService, ILogger<ReminderService> logger, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task SendUnapprovedRequestsReminder()
        {
            _logger.LogInformation("Bắt đầu chạy Job nhắc nhở các yêu cầu chưa được phê duyệt.");

            // Tìm các phiếu có Status == 1 (Khởi tạo)
            var unapprovedRequests = await _context.RequestMaintenances
                .Where(r => r.Status == 1)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            if (!unapprovedRequests.Any())
            {
                _logger.LogInformation("Không có phiếu nào đang chờ phê duyệt. Bỏ qua việc gửi email.");
                return;
            }

            // Lấy danh sách email cấu hình từ ReminderSetting (lấy bản ghi đang active đầu tiên)
            var setting = await _context.ReminderSettings.FirstOrDefaultAsync(s => s.IsActive);

            if (setting == null || string.IsNullOrWhiteSpace(setting.TargetEmails))
            {
                _logger.LogWarning("Không tìm thấy cấu hình ReminderSetting hoặc danh sách email trống.");
                return;
            }

            var emails = setting.TargetEmails.Split(',').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)).ToList();

            if (!emails.Any())
            {
                _logger.LogWarning("Không có địa chỉ email hợp lệ để gửi thông báo.");
                return;
            }

            // Tạo nội dung email tổng hợp
            var emailContent = $"<h3>Thông báo: Có {unapprovedRequests.Count} yêu cầu bảo trì đang chờ phê duyệt</h3>";
            emailContent += "<table border='1' cellpadding='5' cellspacing='0' style='border-collapse: collapse; width: 100%;'>";
            emailContent += "<thead><tr style='background-color: #f2f2f2;'><th>Mã Yêu Cầu (ID)</th><th>Người yêu cầu</th><th>Thiết bị hỏng</th><th>Vị trí</th><th>Thời gian tạo</th></tr></thead><tbody>";

            foreach (var req in unapprovedRequests)
            {
                var localTime = req.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
                emailContent += $"<tr><td>{req.Id}</td><td>{req.FullName}</td><td>{req.EquipmentDamged}</td><td>{req.Location}</td><td>{localTime}</td></tr>";
            }

            emailContent += "</tbody></table>";
            emailContent += "<br/><p>Vui lòng đăng nhập vào hệ thống để kiểm tra và phân công xử lý.</p>";

            // Lấy AppUrl từ appsettings.json, mặc định dùng /Admin/Index (relative) nếu không có
            var appUrl = _configuration["AppUrl"] ?? "https://helpdesk.viaa.edu.vn/";
            var adminLink = $"{appUrl.TrimEnd('/')}/Admin/Index";

            emailContent += $"<div style='margin-top: 20px;'>" +
                            $"<a href='{adminLink}' style='background-color: #0d6efd; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block;'>Đăng nhập để duyệt ngay</a>" +
                            $"</div>";

            // Gửi email cho từng người trong danh sách
            foreach (var email in emails)
            {
                await _emailService.SendEmailAsync(
                    email,
                    $"[Nhắc nhở] Có {unapprovedRequests.Count} yêu cầu bảo trì cần phê duyệt",
                    emailContent);
            }

            _logger.LogInformation($"Đã gửi email nhắc nhở tới {emails.Count} quản trị viên thành công.");
        }
    }
}
