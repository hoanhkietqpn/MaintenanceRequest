using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaintenanceRequestApp.Models
{
    public class AuditLog
    {
        [Key]
        public int LogId { get; set; }

        [Required]
        public Guid RequestId { get; set; }

        [ForeignKey("RequestId")]
        public RequestMaintenance RequestMaintenance { get; set; }

        public string UserId { get; set; }

        [Required]
        [StringLength(255)]
        public string Action { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
