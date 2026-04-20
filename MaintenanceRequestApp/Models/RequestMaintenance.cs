using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MaintenanceRequestApp.Models
{
    public class RequestMaintenance
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "Họ và tên không được để trống.")]
        [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(100, ErrorMessage = "Email không được vượt quá 100 ký tự.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Thiết bị hư hỏng không được để trống.")]
        [StringLength(200, ErrorMessage = "Tên thiết bị không được vượt quá 200 ký tự.")]
        public string EquipmentDamged { get; set; }

        [Required(ErrorMessage = "Mô tả không được để trống.")]
        [StringLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Vị trí không được để trống.")]
        [StringLength(200, ErrorMessage = "Vị trí không được vượt quá 200 ký tự.")]
        public string Location { get; set; }

        [StringLength(200)]
        public string? Urgency { get; set; }

        [Required]
        public int Status { get; set; } = 1; // 1: Khởi tạo, 2: Đã Phê duyệt, 3: Đang sửa chữa, 4: Hoàn thành, 5: Hủy

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        // Navigation Properties
        public ICollection<RequestMedia>? Medias { get; set; }
        public ICollection<RequestAssignment>? Assignments { get; set; }
        public ICollection<AuditLog>? AuditLogs { get; set; }
        public ICollection<MaintenanceNote>? MaintenanceNotes { get; set; }
    }
}
