using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MaintenanceRequestApp.Models
{
    public class MaintenanceNote
    {
        [Key]
        public int NoteId { get; set; }

        [Required]
        public Guid RequestId { get; set; }

        [ForeignKey("RequestId")]
        public RequestMaintenance? RequestMaintenance { get; set; }

        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "Nội dung ghi chú không được để trống.")]
        public string NoteContent { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public bool IsPublicResponse { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
