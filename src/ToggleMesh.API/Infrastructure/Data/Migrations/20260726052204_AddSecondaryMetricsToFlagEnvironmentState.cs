using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecondaryMetricsToFlagEnvironmentState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "SecondaryMetrics",
                table: "FlagEnvironmentStates",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecondaryMetrics",
                table: "FlagEnvironmentStates");
        }
    }
}
