using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FalseCount",
                table: "FeatureFlags",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TrueCount",
                table: "FeatureFlags",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FalseCount",
                table: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "TrueCount",
                table: "FeatureFlags");
        }
    }
}
