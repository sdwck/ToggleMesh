using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Migrations
{
    /// <inheritdoc />
    public partial class Add_MultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeatureFlags_Key",
                table: "FeatureFlags");

            migrationBuilder.AddColumn<Guid>(
                name: "EnvironmentId",
                table: "FeatureFlags",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Environments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Environments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvironmentKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpireOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvironmentKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvironmentKeys_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_EnvironmentId_Key",
                table: "FeatureFlags",
                columns: new[] { "EnvironmentId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentKeys_ApiKey",
                table: "EnvironmentKeys",
                column: "ApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnvironmentKeys_EnvironmentId",
                table: "EnvironmentKeys",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_ProjectId",
                table: "Environments",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeatureFlags_Environments_EnvironmentId",
                table: "FeatureFlags",
                column: "EnvironmentId",
                principalTable: "Environments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeatureFlags_Environments_EnvironmentId",
                table: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "EnvironmentKeys");

            migrationBuilder.DropTable(
                name: "Environments");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_FeatureFlags_EnvironmentId_Key",
                table: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "EnvironmentId",
                table: "FeatureFlags");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Key",
                table: "FeatureFlags",
                column: "Key",
                unique: true);
        }
    }
}
