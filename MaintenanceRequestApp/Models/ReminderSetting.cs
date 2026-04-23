using System.ComponentModel.DataAnnotations;

namespace MaintenanceRequestApp.Models
{
    public class ReminderSetting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string CronExpression { get; set; } = "0 10,15,21 * * *"; // Mặc định 10h, 15h, 21h

        [Required]
        public string TargetEmails { get; set; } = string.Empty; // Danh sách email admin nhận thông báo, cách nhau bằng dấu phẩy

        public bool IsActive { get; set; } = true;
    }
}
