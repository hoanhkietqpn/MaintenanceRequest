using System.Threading.Tasks;

namespace MaintenanceRequestApp.Services
{
    public interface IReminderService
    {
        Task SendUnapprovedRequestsReminder();
    }
}
