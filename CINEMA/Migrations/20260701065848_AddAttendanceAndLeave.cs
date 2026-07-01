using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CINEMA.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceAndLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Admins_AdminId",
                table: "ActivityLogs");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Admins",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Avatar",
                table: "Admins",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BirthDate",
                table: "Admins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Admins",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenId",
                table: "Admins",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "Admins",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Admins",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HireDate",
                table: "Admins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobInfo",
                table: "Admins",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PositionId",
                table: "Admins",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Admins",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LogDate",
                table: "ActivityLogs",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "Entity",
                table: "ActivityLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "ActivityLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "Attendance",
                columns: table => new
                {
                    AttendanceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    CheckOutTime = table.Column<DateTime>(type: "datetime", nullable: true),
                    CheckInPhoto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CheckOutPhoto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendance", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_Attendance_Admins_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Admins",
                        principalColumn: "AdminId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    BranchId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TheaterId = table.Column<int>(type: "int", nullable: false),
                    BranchCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.BranchId);
                    table.ForeignKey(
                        name: "FK_Branches_Theaters_TheaterId",
                        column: x => x.TheaterId,
                        principalTable: "Theaters",
                        principalColumn: "TheaterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Chờ duyệt"),
                    CreatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Admins_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Admins",
                        principalColumn: "AdminId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    PositionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PositionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SalaryCoefficient = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.PositionId);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShiftName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.ShiftId);
                });

            migrationBuilder.CreateTable(
                name: "WorkSchedules",
                columns: table => new
                {
                    ScheduleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedules", x => x.ScheduleId);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_Admins_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Admins",
                        principalColumn: "AdminId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkSchedules_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admins_BranchId",
                table: "Admins",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Admins_PositionId",
                table: "Admins",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_AdminId",
                table: "Attendance",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TheaterId",
                table: "Branches",
                column: "TheaterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_AdminId",
                table: "LeaveRequests",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_AdminId",
                table: "WorkSchedules",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_ShiftId",
                table: "WorkSchedules",
                column: "ShiftId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Admins_AdminId",
                table: "ActivityLogs",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Admins_Branches_BranchId",
                table: "Admins",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Admins_Positions_PositionId",
                table: "Admins",
                column: "PositionId",
                principalTable: "Positions",
                principalColumn: "PositionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Admins_AdminId",
                table: "ActivityLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Admins_Branches_BranchId",
                table: "Admins");

            migrationBuilder.DropForeignKey(
                name: "FK_Admins_Positions_PositionId",
                table: "Admins");

            migrationBuilder.DropTable(
                name: "Attendance");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Admins_BranchId",
                table: "Admins");

            migrationBuilder.DropIndex(
                name: "IX_Admins_PositionId",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "CitizenId",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "HireDate",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "JobInfo",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "PositionId",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Admins");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LogDate",
                table: "ActivityLogs",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<string>(
                name: "Entity",
                table: "ActivityLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "ActivityLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Admins_AdminId",
                table: "ActivityLogs",
                column: "AdminId",
                principalTable: "Admins",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
