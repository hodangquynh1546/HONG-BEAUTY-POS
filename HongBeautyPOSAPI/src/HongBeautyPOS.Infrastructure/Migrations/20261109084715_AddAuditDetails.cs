using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HongBeautyPOS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetailsJson",
                table: "audit_events",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailsJson",
                table: "audit_events");
        }
    }
}
