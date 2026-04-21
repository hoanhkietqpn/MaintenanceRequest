using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceRequestApp.Migrations
{
    /// <inheritdoc />
    public partial class UpdateKPIModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceNotes_Users_UserId",
                table: "MaintenanceNotes");

            migrationBuilder.AlterColumn<string>(
                name: "Urgency",
                table: "RequestMaintenances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "MaintenanceNotes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceNotes_Users_UserId",
                table: "MaintenanceNotes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceNotes_Users_UserId",
                table: "MaintenanceNotes");

            migrationBuilder.AlterColumn<string>(
                name: "Urgency",
                table: "RequestMaintenances",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "MaintenanceNotes",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceNotes_Users_UserId",
                table: "MaintenanceNotes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
