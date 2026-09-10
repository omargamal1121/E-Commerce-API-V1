using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updateproductFitType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropIndex(
            //    name: "IX_Payments_OrderId_Status_PaymentMethodId",
            //    table: "Payments");

            //migrationBuilder.DropIndex(
            //    name: "IX_Orders_Status",
            //    table: "Orders");

            //migrationBuilder.DropIndex(
            //    name: "IX_CartItems_CartId_ProductId_ProductVariantId",
            //    table: "CartItems");

            migrationBuilder.AddColumn<int>(
                name: "Chest",
                table: "ProductVariants",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "fitType",
                table: "Products",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId",
                table: "CartItems",
                column: "CartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropIndex(
            //    name: "IX_CartItems_CartId",
            //    table: "CartItems");

            migrationBuilder.DropColumn(
                name: "Chest",
                table: "ProductVariants");

            migrationBuilder.AlterColumn<int>(
                name: "fitType",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_Status_PaymentMethodId",
                table: "Payments",
                columns: new[] { "OrderId", "Status", "PaymentMethodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status",
                table: "Orders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_ProductId_ProductVariantId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId", "ProductVariantId" },
                unique: true);
        }
    }
}
