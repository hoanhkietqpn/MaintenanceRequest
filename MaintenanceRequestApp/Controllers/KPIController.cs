using Microsoft.AspNetCore.Mvc;
using MaintenanceRequestApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using MaintenanceRequestApp.ViewModels;
using System.Collections.Generic;

namespace MaintenanceRequestApp.Controllers
{
    [Authorize(Roles = "Admin,QuanLyKyThuat")]
    public class KPIController : Controller
    {
        private readonly MaintenanceDbContext _context;

        public KPIController(MaintenanceDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string staffId)
        {
            var vm = new KPIReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                StaffId = staffId
            };

            // Fetch technical staff for the dropdown
            ViewBag.StaffList = await _context.Users
                .Where(u => u.Role == "NhanVienKyThuat" || u.Role == "QuanLyKyThuat")
                .ToListAsync();

            // Query completed requests with StartTime and EndTime
            var completedRequestsQuery = _context.RequestMaintenances
                .Include(r => r.Assignments)
                .ThenInclude(a => a.User)
                .Where(r => r.Status == 4 && r.StartTime.HasValue && r.EndTime.HasValue);

            if (fromDate.HasValue)
            {
                // Filter by completed (End Time) after fromDate
                completedRequestsQuery = completedRequestsQuery.Where(r => r.EndTime >= fromDate.Value.ToUniversalTime());
            }

            if (toDate.HasValue)
            {
                var upperLimit = toDate.Value.Date.AddDays(1).ToUniversalTime();
                completedRequestsQuery = completedRequestsQuery.Where(r => r.EndTime < upperLimit);
            }

            if (!string.IsNullOrEmpty(staffId))
            {
                completedRequestsQuery = completedRequestsQuery.Where(r => r.Assignments.Any(a => a.UserId == staffId));
            }

            var requests = await completedRequestsQuery.ToListAsync();

            // Build task details
            foreach (var req in requests)
            {
                var duration = (req.EndTime.Value - req.StartTime.Value).TotalHours;
                
                string assignedNames = string.Join(", ", req.Assignments.Select(a => $"{a.User.FirstName} {a.User.LastName}"));

                vm.TaskDetails.Add(new KPITaskDetail
                {
                    RequestId = req.Id,
                    TaskTitle = req.EquipmentDamged,
                    Location = req.Location,
                    AssignedStaffName = string.IsNullOrEmpty(assignedNames) ? "N/A" : assignedNames,
                    StartTime = req.StartTime.Value.ToLocalTime(),
                    EndTime = req.EndTime.Value.ToLocalTime(),
                    DurationHours = Math.Round(duration, 2)
                });
            }

            // Build Staff Summary
            var staffSummaryDict = new Dictionary<string, StaffKPISummary>();

            foreach (var req in requests)
            {
                var duration = (req.EndTime.Value - req.StartTime.Value).TotalHours;

                foreach (var assign in req.Assignments)
                {
                    if (!string.IsNullOrEmpty(staffId) && assign.UserId != staffId) continue;

                    if (!staffSummaryDict.ContainsKey(assign.UserId))
                    {
                        staffSummaryDict[assign.UserId] = new StaffKPISummary
                        {
                            UserId = assign.UserId,
                            StaffName = $"{assign.User.FirstName} {assign.User.LastName}",
                            TotalTasksCompleted = 0,
                            TotalWorkHours = 0,
                            AverageCompletionTimeHours = 0
                        };
                    }

                    var summary = staffSummaryDict[assign.UserId];
                    summary.TotalTasksCompleted++;
                    summary.TotalWorkHours += duration;
                }
            }

            // Calculate actual averages and team stats
            vm.TotalTasksCompleted = requests.Count;
            vm.TotalTeamHours = 0;
            double maxHours = -1;

            foreach (var summary in staffSummaryDict.Values)
            {
                vm.TotalTeamHours += summary.TotalWorkHours;
                if (summary.TotalWorkHours > maxHours)
                {
                    maxHours = summary.TotalWorkHours;
                    vm.TopStaffName = summary.StaffName;
                }

                if (summary.TotalTasksCompleted > 0)
                {
                    summary.AverageCompletionTimeHours = Math.Round(summary.TotalWorkHours / summary.TotalTasksCompleted, 2);
                    summary.TotalWorkHours = Math.Round(summary.TotalWorkHours, 1);
                }
            }

            vm.StaffSummaries = staffSummaryDict.Values.OrderByDescending(s => s.TotalWorkHours).ToList();
            vm.TaskDetails = vm.TaskDetails.OrderByDescending(t => t.EndTime).ToList();

            return View(vm);
        }
    }
}
