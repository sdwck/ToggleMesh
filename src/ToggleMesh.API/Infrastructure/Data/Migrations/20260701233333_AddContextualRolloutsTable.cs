using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContextualRolloutsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextualRollouts",
                table: "FlagEnvironmentStates");

            migrationBuilder.AddColumn<Guid>(
                name: "RolloutId",
                table: "ContextualExperimentMetrics",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContextualRollouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlagEnvironmentStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextSlice = table.Column<string>(type: "text", nullable: false),
                    RolloutPercentage = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextualRollouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContextualRollouts_FlagEnvironmentStates_FlagEnvironmentSta~",
                        column: x => x.FlagEnvironmentStateId,
                        principalTable: "FlagEnvironmentStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContextualRollouts_FlagEnvironmentStateId",
                table: "ContextualRollouts",
                column: "FlagEnvironmentStateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContextualRollouts");

            migrationBuilder.DropColumn(
                name: "RolloutId",
                table: "ContextualExperimentMetrics");

            migrationBuilder.AddColumn<Dictionary<string, int>>(
                name: "ContextualRollouts",
                table: "FlagEnvironmentStates",
                type: "jsonb",
                nullable: true);
        }
    }
}
