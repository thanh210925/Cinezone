using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CINEMA.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherRulesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinGroupMembers",
                table: "VoucherConditions");

            migrationBuilder.DropColumn(
                name: "RequireCombo",
                table: "VoucherConditions");

            migrationBuilder.DropColumn(
                name: "RequireGroupBooking",
                table: "VoucherConditions");

            migrationBuilder.DropColumn(
                name: "RequireTicket",
                table: "VoucherConditions");

            migrationBuilder.CreateTable(
                name: "VoucherRules",
                columns: table => new
                {
                    VoucherRuleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherConditionId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherRules", x => x.VoucherRuleId);
                    table.ForeignKey(
                        name: "FK_VoucherRules_VoucherConditions_VoucherConditionId",
                        column: x => x.VoucherConditionId,
                        principalTable: "VoucherConditions",
                        principalColumn: "VoucherConditionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRules_VoucherConditionId",
                table: "VoucherRules",
                column: "VoucherConditionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoucherRules");

            migrationBuilder.AddColumn<int>(
                name: "MinGroupMembers",
                table: "VoucherConditions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireCombo",
                table: "VoucherConditions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireGroupBooking",
                table: "VoucherConditions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireTicket",
                table: "VoucherConditions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
