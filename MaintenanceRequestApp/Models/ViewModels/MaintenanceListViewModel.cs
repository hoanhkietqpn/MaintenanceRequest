using System;
using MaintenanceRequestApp.Models;
using MaintenanceRequestApp.Helpers;

namespace MaintenanceRequestApp.ViewModels
{
    public class MaintenanceListViewModel
    {
        public PaginatedList<RequestMaintenance> Requests { get; set; }
        
        // Filter parameters
        public int? Status { get; set; }
        public string SearchTerm { get; set; }
        public string StaffId { get; set; }
    }
}
