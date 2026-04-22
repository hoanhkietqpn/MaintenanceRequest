using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceRequestApp.Migrations
{
    /// <inheritdoc />
    public partial class AddIsCompletionResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCompletionResponse",
                table: "MaintenanceNotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCompletionResponse",
                table: "MaintenanceNotes");
        }
    }
}
