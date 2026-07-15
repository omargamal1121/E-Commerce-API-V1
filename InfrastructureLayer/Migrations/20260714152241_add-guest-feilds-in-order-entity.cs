using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addguestfeildsinorderentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestTokenHash",
                table: "Orders",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "Orders",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Status_PaymentMethodId",
                table: "Payments",
                columns: new[] { "OrderId", "Status", "PaymentMethodId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_OrderId_Status_PaymentMethodId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GuestTokenHash",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "Orders");
        }
    }
}
