using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFeatureFlagUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectFeatureFlags_ProjectId_Key",
                table: "ProjectFeatureFlags");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFeatureFlags_ProjectId_Key",
                table: "ProjectFeatureFlags",
                columns: new[] { "ProjectId", "Key" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectFeatureFlags_ProjectId_Key",
                table: "ProjectFeatureFlags");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectFeatureFlags_ProjectId_Key",
                table: "ProjectFeatureFlags",
                columns: new[] { "ProjectId", "Key" },
                unique: true);
        }
    }
}
