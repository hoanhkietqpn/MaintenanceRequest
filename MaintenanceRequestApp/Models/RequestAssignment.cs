using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaintenanceRequestApp.Models
{
    public class RequestAssignment
    {
        [Key]
        public int AssignmentId { get; set; }

        [Required]
        public Guid RequestId { get; set; }

        [ForeignKey("RequestId")]
        public RequestMaintenance RequestMaintenance { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }
    }
}
