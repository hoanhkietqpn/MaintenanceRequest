using System;
using System.Collections.Generic;
using MaintenanceRequestApp.Models;

namespace MaintenanceRequestApp.ViewModels
{
    public class KPIReportViewModel
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string StaffId { get; set; }

        public List<StaffKPISummary> StaffSummaries { get; set; } = new List<StaffKPISummary>();
        public List<KPITaskDetail> TaskDetails { get; set; } = new List<KPITaskDetail>();

        // Team-wide stats
        public double TotalTeamHours { get; set; }
        public string TopStaffName { get; set; }
        public int TotalTasksCompleted { get; set; }

        public string FormattedTeamTime => FormatTime(TotalTeamHours);

        public static string FormatTime(double totalHours)
        {
            var hours = (int)totalHours;
            var minutes = (int)((totalHours - hours) * 60);
            if (hours == 0 && minutes == 0) return "0h";
            if (hours == 0) return $"{minutes}m";
            if (minutes == 0) return $"{hours}h";
            return $"{hours}h {minutes}m";
        }
    }

    public class StaffKPISummary
    {
        public string UserId { get; set; }
        public string StaffName { get; set; }
        public int TotalTasksCompleted { get; set; }
        public double TotalWorkHours { get; set; }
        public double AverageCompletionTimeHours { get; set; }

        public string FormattedTotalTime => KPIReportViewModel.FormatTime(TotalWorkHours);
        public string FormattedAverageTime => KPIReportViewModel.FormatTime(AverageCompletionTimeHours);
    }

    public class KPITaskDetail
    {
        public Guid RequestId { get; set; }
        public string TaskTitle { get; set; }
        public string Location { get; set; }
        public string AssignedStaffName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double DurationHours { get; set; }
        public bool IsOver24Hours => DurationHours > 24;
    }
}
