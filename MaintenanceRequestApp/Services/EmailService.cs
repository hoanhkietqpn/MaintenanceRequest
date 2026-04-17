using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MaintenanceRequestApp.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var portStr = _configuration["EmailSettings:Port"];
                var port = string.IsNullOrEmpty(portStr) ? 587 : int.Parse(portStr);
                var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "no-reply@vjaa.edu.vn";
                var senderName = _configuration["EmailSettings:SenderName"] ?? "Hệ thống quản lý sự cố";
                var password = _configuration["EmailSettings:Password"];

                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("Chưa cấu hình App Password cho Email, bỏ qua thao tác gửi mail. Tới email: " + toEmail);
                    return;
                }

                using var message = new MailMessage();
                message.From = new MailAddress(senderEmail, senderName);
                message.To.Add(new MailAddress(toEmail));
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(smtpServer, port);
                client.Credentials = new NetworkCredential(senderEmail, password);
                client.EnableSsl = true;

                await client.SendMailAsync(message);
                _logger.LogInformation($"Đã gửi email thành công đến {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi khi gửi email đến {toEmail}");
            }
        }
    }
}
