using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HongBeautyPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProductAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attribute_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ValueKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_options", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_attribute_selections",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attribute_selections", x => new { x.ProductId, x.OptionId });
                    table.ForeignKey(
                        name: "FK_product_attribute_selections_attribute_options_OptionId",
                        column: x => x.OptionId,
                        principalTable: "attribute_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_attribute_selections_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attribute_options_NameKey_ValueKey",
                table: "attribute_options",
                columns: new[] { "NameKey", "ValueKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_attribute_selections_OptionId",
                table: "product_attribute_selections",
                column: "OptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_attribute_selections");

            migrationBuilder.DropTable(
                name: "attribute_options");
        }
    }
}
