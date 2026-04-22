using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceRequestApp.Migrations
{
    /// <inheritdoc />
    public partial class RenameIsPublicResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsCompletionResponse",
                table: "MaintenanceNotes",
                newName: "IsPublicResponse");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPublicResponse",
                table: "MaintenanceNotes",
                newName: "IsCompletionResponse");
        }
    }
}
