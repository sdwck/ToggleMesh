using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlagConfigSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlagConfigSnapshot",
                table: "ExperimentIterations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlagConfigSnapshot",
                table: "ExperimentIterations");
        }
    }
}
