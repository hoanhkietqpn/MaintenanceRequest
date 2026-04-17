using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaintenanceRequestApp.Models
{
    public class RequestMedia
    {
        [Key]
        public int MediaId { get; set; }

        [Required]
        public Guid RequestId { get; set; }

        [ForeignKey("RequestId")]
        public RequestMaintenance RequestMaintenance { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
