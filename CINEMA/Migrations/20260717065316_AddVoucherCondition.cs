using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CINEMA.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherCondition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicableScope",
                table: "Vouchers");

            migrationBuilder.AlterColumn<string>(
                name: "TermsAndConditions",
                table: "Vouchers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "VoucherConditionId",
                table: "Vouchers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VoucherConditions",
                columns: table => new
                {
                    VoucherConditionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequireTicket = table.Column<bool>(type: "bit", nullable: false),
                    RequireCombo = table.Column<bool>(type: "bit", nullable: false),
                    RequireGroupBooking = table.Column<bool>(type: "bit", nullable: false),
                    MinGroupMembers = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherConditions", x => x.VoucherConditionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_VoucherConditionId",
                table: "Vouchers",
                column: "VoucherConditionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_VoucherConditions_VoucherConditionId",
                table: "Vouchers",
                column: "VoucherConditionId",
                principalTable: "VoucherConditions",
                principalColumn: "VoucherConditionId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_VoucherConditions_VoucherConditionId",
                table: "Vouchers");

            migrationBuilder.DropTable(
                name: "VoucherConditions");

            migrationBuilder.DropIndex(
                name: "IX_Vouchers_VoucherConditionId",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "VoucherConditionId",
                table: "Vouchers");

            migrationBuilder.AlterColumn<string>(
                name: "TermsAndConditions",
                table: "Vouchers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicableScope",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
