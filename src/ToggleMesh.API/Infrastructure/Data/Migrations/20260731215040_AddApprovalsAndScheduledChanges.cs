using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalsAndScheduledChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProtected",
                table: "ProjectFeatureFlags",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovals",
                table: "Environments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireForProtectedFlagsOnly",
                table: "Environments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovalsCount",
                table: "Environments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PendingFlagChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlagId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedByUserIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PatchInstructionsJson = table.Column<string>(type: "text", nullable: false),
                    ExecuteAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingFlagChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingFlagChanges_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingFlagChanges_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingFlagChanges_ProjectFeatureFlags_FlagId",
                        column: x => x.FlagId,
                        principalTable: "ProjectFeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingFlagChanges_EnvironmentId",
                table: "PendingFlagChanges",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingFlagChanges_FlagId",
                table: "PendingFlagChanges",
                column: "FlagId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingFlagChanges_RequestedByUserId",
                table: "PendingFlagChanges",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingFlagChanges");

            migrationBuilder.DropColumn(
                name: "IsProtected",
                table: "ProjectFeatureFlags");

            migrationBuilder.DropColumn(
                name: "RequireApprovals",
                table: "Environments");

            migrationBuilder.DropColumn(
                name: "RequireForProtectedFlagsOnly",
                table: "Environments");

            migrationBuilder.DropColumn(
                name: "RequiredApprovalsCount",
                table: "Environments");
        }
    }
}
