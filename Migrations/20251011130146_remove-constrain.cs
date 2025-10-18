using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E_Commerce.Migrations
{
    /// <inheritdoc />
    public partial class removeconstrain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductVariant_Quantity_NonNegative",
                table: "ProductVariants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_Price_Positive",
                table: "Products");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_Quantity_NonNegative",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductVariant_Quantity_NonNegative",
                table: "ProductVariants",
                sql: "'Quantity'>= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_Quantity_NonNegative",
                table: "Products",
                sql: "'Quantity' >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProductVariant_Quantity_NonNegative",
                table: "ProductVariants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Product_Quantity_NonNegative",
                table: "Products");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProductVariant_Quantity_NonNegative",
                table: "ProductVariants",
                sql: "[Quantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_Price_Positive",
                table: "Products",
                sql: "[Price] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Product_Quantity_NonNegative",
                table: "Products",
                sql: "[Quantity] >= 0");
        }
    }
}
