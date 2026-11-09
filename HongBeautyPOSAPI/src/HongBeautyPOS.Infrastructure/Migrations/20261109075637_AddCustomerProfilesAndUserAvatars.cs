using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HongBeautyPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerProfilesAndUserAvatars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarData",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "customers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarData",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "customers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "customers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "customers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone2",
                table: "customers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customers_GroupId",
                table: "customers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_groups_Name",
                table: "customer_groups",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_customers_customer_groups_GroupId",
                table: "customers",
                column: "GroupId",
                principalTable: "customer_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_customer_groups_GroupId",
                table: "customers");

            migrationBuilder.DropTable(
                name: "customer_groups");

            migrationBuilder.DropIndex(
                name: "IX_customers_GroupId",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AvatarData",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "AvatarData",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Contact",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Phone2",
                table: "customers");
        }
    }
}
