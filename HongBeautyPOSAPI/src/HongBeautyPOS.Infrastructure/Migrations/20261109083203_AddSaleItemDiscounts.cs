using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HongBeautyPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleItemDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "sales_order_items",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "sales_order_items",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_sales_order_items_discount",
                table: "sales_order_items",
                sql: "\"DiscountPercent\" >= 0 AND \"DiscountPercent\" <= 100 AND \"DiscountAmount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sales_order_items_discount",
                table: "sales_order_items");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "sales_order_items");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "sales_order_items");
        }
    }
}
