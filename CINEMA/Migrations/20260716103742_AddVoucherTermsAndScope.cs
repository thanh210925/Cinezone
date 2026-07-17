using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CINEMA.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherTermsAndScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicableScope",
                table: "Vouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TermsAndConditions",
                table: "Vouchers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReputationScore",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GroupBookingRooms",
                columns: table => new
                {
                    RoomId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ShowtimeId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Waiting"),
                    MaxMembers = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupBookingRooms", x => x.RoomId);
                    table.ForeignKey(
                        name: "FK_GroupBookingRooms_Customers_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupBookingRooms_Showtimes_ShowtimeId",
                        column: x => x.ShowtimeId,
                        principalTable: "Showtimes",
                        principalColumn: "ShowtimeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupBookingMembers",
                columns: table => new
                {
                    MemberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    SeatId = table.Column<int>(type: "int", nullable: true),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Joined")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupBookingMembers", x => x.MemberId);
                    table.ForeignKey(
                        name: "FK_GroupBookingMembers_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GroupBookingMembers_GroupBookingRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "GroupBookingRooms",
                        principalColumn: "RoomId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupBookingMembers_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GroupBookingMembers_Seats_SeatId",
                        column: x => x.SeatId,
                        principalTable: "Seats",
                        principalColumn: "SeatId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GroupBookingMembers_CustomerId",
                table: "GroupBookingMembers",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBookingMembers_OrderId",
                table: "GroupBookingMembers",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBookingMembers_RoomId",
                table: "GroupBookingMembers",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBookingMembers_SeatId",
                table: "GroupBookingMembers",
                column: "SeatId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBookingRooms_CreatedBy",
                table: "GroupBookingRooms",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_GroupBookingRooms_ShowtimeId",
                table: "GroupBookingRooms",
                column: "ShowtimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupBookingMembers");

            migrationBuilder.DropTable(
                name: "GroupBookingRooms");

            migrationBuilder.DropColumn(
                name: "ApplicableScope",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "TermsAndConditions",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "ReputationScore",
                table: "Customers");
        }
    }
}
