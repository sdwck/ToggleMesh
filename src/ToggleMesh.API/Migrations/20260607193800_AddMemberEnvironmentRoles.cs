using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberEnvironmentRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedEnvironmentIds",
                table: "ProjectMembers");

            migrationBuilder.CreateTable(
                name: "MemberEnvironmentRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberEnvironmentRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberEnvironmentRoles_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberEnvironmentRoles_ProjectMembers_ProjectMemberId",
                        column: x => x.ProjectMemberId,
                        principalTable: "ProjectMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemberEnvironmentRoles_EnvironmentId",
                table: "MemberEnvironmentRoles",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberEnvironmentRoles_ProjectMemberId_EnvironmentId",
                table: "MemberEnvironmentRoles",
                columns: new[] { "ProjectMemberId", "EnvironmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemberEnvironmentRoles");

            migrationBuilder.AddColumn<List<Guid>>(
                name: "AllowedEnvironmentIds",
                table: "ProjectMembers",
                type: "uuid[]",
                nullable: true);
        }
    }
}
