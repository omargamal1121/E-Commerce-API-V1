using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class updatepaymen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. احذف الـ duplicates لو فيه
            migrationBuilder.Sql(@"
    DELETE FROM Payments
    WHERE Id NOT IN (
        SELECT Id FROM (
            SELECT MIN(Id) AS Id
            FROM Payments
            GROUP BY OrderId, Status, PaymentMethodId
        ) AS tmp
    );
");

            // 2. اعمل الـ Unique Index
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
        }
    }
}
