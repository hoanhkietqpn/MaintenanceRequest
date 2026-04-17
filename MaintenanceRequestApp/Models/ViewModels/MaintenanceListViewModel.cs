using System;
using System.Collections.Generic;
using MaintenanceRequestApp.Models;

namespace MaintenanceRequestApp.ViewModels
{
    public class MaintenanceListViewModel
    {
        public IEnumerable<RequestMaintenance> Requests { get; set; } = new List<RequestMaintenance>();
        
        // Filter parameters
        public int? Status { get; set; }
        public string SearchTerm { get; set; }
        public string StaffId { get; set; }
        
        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
    }
}
