using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSkipLandingPageToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SkipLandingPage",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkipLandingPage",
                table: "AspNetUsers");
        }
    }
}
