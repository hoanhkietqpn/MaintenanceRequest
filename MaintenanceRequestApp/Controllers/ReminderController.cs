using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using MaintenanceRequestApp.Data;
using MaintenanceRequestApp.Models;
using MaintenanceRequestApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceRequestApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ReminderController : Controller
    {
        private readonly MaintenanceDbContext _context;
        private readonly IReminderService _reminderService;

        public ReminderController(MaintenanceDbContext context, IReminderService reminderService)
        {
            _context = context;
            _reminderService = reminderService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var setting = await _context.ReminderSettings.FirstOrDefaultAsync() ?? new ReminderSetting();
            return View(setting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ReminderSetting model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            var setting = await _context.ReminderSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new ReminderSetting();
                _context.ReminderSettings.Add(setting);
            }

            setting.CronExpression = model.CronExpression;
            setting.TargetEmails = model.TargetEmails;
            setting.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            // Cập nhật lại Lịch Hangfire
            UpdateHangfireJob(setting);

            TempData["SuccessMessage"] = "Cập nhật cấu hình nhắc nhở thành công!";
            return RedirectToAction(nameof(Index));
        }

        private void UpdateHangfireJob(ReminderSetting setting)
        {
            var jobId = "SendUnapprovedRequestsReminderJob";

            if (setting.IsActive && !string.IsNullOrWhiteSpace(setting.CronExpression))
            {
                TimeZoneInfo tz;
                try
                {
                    tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch
                {
                    // Fallback in case IANA timezone is not found on Windows
                    try { tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
                    catch { tz = TimeZoneInfo.Local; }
                }

                RecurringJob.AddOrUpdate(
                    jobId,
                    () => _reminderService.SendUnapprovedRequestsReminder(),
                    setting.CronExpression,
                    new RecurringJobOptions { TimeZone = tz }
                );
            }
            else
            {
                RecurringJob.RemoveIfExists(jobId);
            }
        }
    }
}
