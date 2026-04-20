using System.Collections.Generic;
using MaintenanceRequestApp.Models;
using MaintenanceRequestApp.Helpers;

namespace MaintenanceRequestApp.ViewModels
{
    public class UserListViewModel
    {
        public PaginatedList<User> Users { get; set; }
        public string SearchTerm { get; set; }
    }
}
