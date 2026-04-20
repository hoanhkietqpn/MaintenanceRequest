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
            if (string.IsNullOrEmpty(toEmail)) return;

            try
            {
                var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                var port = int.TryParse(_configuration["EmailSettings:Port"], out var p) ? p : 587;
                var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
                var senderName = _configuration["EmailSettings:SenderName"] ?? "Hệ thống VIAA";
                var password = _configuration["EmailSettings:Password"];

                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("⚠️ [Email] Chưa cấu hình Password SMTP — bỏ qua gửi đến: {ToEmail}", toEmail);
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
                client.Timeout = 15000;

                await client.SendMailAsync(message);
                _logger.LogInformation("✅ [Email] Đã gửi thành công đến: {ToEmail} | {Subject}", toEmail, subject);
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError("❌ [Email] Lỗi SMTP ({Status}): {Message} | Inner: {Inner}",
                    smtpEx.StatusCode, smtpEx.Message, smtpEx.InnerException?.Message ?? "N/A");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [Email] Lỗi gửi đến {ToEmail}: {Message}", toEmail, ex.Message);
            }
        }
    }
}
