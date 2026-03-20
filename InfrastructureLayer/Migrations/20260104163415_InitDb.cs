using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfrastructureLayer.Migrations
{
    /// <inheritdoc />
    public partial class InitDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropIndex(
            //    name: "IX_Payments_OrderId_Status_PaymentMethodId",
            //    table: "Payments");

            //migrationBuilder.CreateIndex(
            //    name: "IX_Payments_OrderId_PaymentMethodId",
            //    table: "Payments",
            //    columns: new[] { "OrderId", "PaymentMethodId" },
            //    unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropIndex(
            //    name: "IX_Payments_OrderId_PaymentMethodId",
            //    table: "Payments");

            //migrationBuilder.CreateIndex(
            //    name: "IX_Payments_OrderId_Status_PaymentMethodId",
            //    table: "Payments",
            //    columns: new[] { "OrderId", "Status", "PaymentMethodId" },
            //    unique: true);
        }
    }
}
