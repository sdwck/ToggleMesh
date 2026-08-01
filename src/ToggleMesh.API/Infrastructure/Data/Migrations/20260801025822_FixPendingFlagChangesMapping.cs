using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingFlagChangesMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PendingFlagChanges_ProjectFeatureFlags_FeatureFlagId",
                table: "PendingFlagChanges");

            migrationBuilder.DropIndex(
                name: "IX_PendingFlagChanges_FeatureFlagId",
                table: "PendingFlagChanges");

            migrationBuilder.DropColumn(
                name: "FeatureFlagId",
                table: "PendingFlagChanges");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FeatureFlagId",
                table: "PendingFlagChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingFlagChanges_FeatureFlagId",
                table: "PendingFlagChanges",
                column: "FeatureFlagId");

            migrationBuilder.AddForeignKey(
                name: "FK_PendingFlagChanges_ProjectFeatureFlags_FeatureFlagId",
                table: "PendingFlagChanges",
                column: "FeatureFlagId",
                principalTable: "ProjectFeatureFlags",
                principalColumn: "Id");
        }
    }
}
