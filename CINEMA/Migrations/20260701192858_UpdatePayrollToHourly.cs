using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CINEMA.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePayrollToHourly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidLeaveDays",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "WorkingDays",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "BaseSalaryPerDay",
                table: "Payrolls",
                newName: "WorkingHours");

            migrationBuilder.AddColumn<decimal>(
                name: "BaseSalaryPerHour",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidLeaveHours",
                table: "Payrolls",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseSalaryPerHour",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "PaidLeaveHours",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "WorkingHours",
                table: "Payrolls",
                newName: "BaseSalaryPerDay");

            migrationBuilder.AddColumn<int>(
                name: "PaidLeaveDays",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WorkingDays",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
