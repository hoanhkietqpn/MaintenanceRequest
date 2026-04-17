using System.Threading.Tasks;

namespace MaintenanceRequestApp.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}
