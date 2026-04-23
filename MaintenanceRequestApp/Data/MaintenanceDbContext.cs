using Microsoft.EntityFrameworkCore;
using MaintenanceRequestApp.Models;

namespace MaintenanceRequestApp.Data
{
    public class MaintenanceDbContext : DbContext
    {
        public MaintenanceDbContext(DbContextOptions<MaintenanceDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<RequestMaintenance> RequestMaintenances { get; set; }
        public DbSet<RequestMedia> RequestMedias { get; set; }
        public DbSet<RequestAssignment> RequestAssignments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<MaintenanceNote> MaintenanceNotes { get; set; }
        public DbSet<ReminderSetting> ReminderSettings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
