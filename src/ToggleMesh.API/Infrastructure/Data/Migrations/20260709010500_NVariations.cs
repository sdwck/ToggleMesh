using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NVariations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExperimentMetrics_EnvironmentId_FlagKey_EventName_Variant",
                table: "ExperimentMetrics");

            migrationBuilder.DropColumn(
                name: "RolloutPercentage",
                table: "FlagEnvironmentStates");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "ExperimentMetrics");

            migrationBuilder.DropColumn(
                name: "RolloutPercentage",
                table: "ContextualRollouts");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "ContextualExperimentMetrics");

            migrationBuilder.DropColumn(
                name: "Variant",
                table: "AnalyticsExposures");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "ProjectFlagRules",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ProjectFlagRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Rollout",
                table: "ProjectFlagRules",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ProjectFeatureFlags",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FallthroughRollout",
                table: "FlagEnvironmentStates",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OffVariationId",
                table: "FlagEnvironmentStates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VariationId",
                table: "ExperimentMetrics",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Rollout",
                table: "ContextualRollouts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VariationId",
                table: "ContextualExperimentMetrics",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "VariationId",
                table: "AnalyticsExposures",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "FlagVariations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureFlagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlagVariations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlagVariations_ProjectFeatureFlags_FeatureFlagId",
                        column: x => x.FeatureFlagId,
                        principalTable: "ProjectFeatureFlags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlagIndividualTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FlagEnvironmentStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    VariationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlagIndividualTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlagIndividualTargets_FlagEnvironmentStates_FlagEnvironment~",
                        column: x => x.FlagEnvironmentStateId,
                        principalTable: "FlagEnvironmentStates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlagIndividualTargets_FlagVariations_VariationId",
                        column: x => x.VariationId,
                        principalTable: "FlagVariations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentMetrics_EnvironmentId_FlagKey_EventName_Variation~",
                table: "ExperimentMetrics",
                columns: new[] { "EnvironmentId", "FlagKey", "EventName", "VariationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlagIndividualTargets_FlagEnvironmentStateId_IdentityKey",
                table: "FlagIndividualTargets",
                columns: new[] { "FlagEnvironmentStateId", "IdentityKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlagIndividualTargets_VariationId",
                table: "FlagIndividualTargets",
                column: "VariationId");

            migrationBuilder.CreateIndex(
                name: "IX_FlagVariations_FeatureFlagId_Key",
                table: "FlagVariations",
                columns: new[] { "FeatureFlagId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlagIndividualTargets");

            migrationBuilder.DropTable(
                name: "FlagVariations");

            migrationBuilder.DropIndex(
                name: "IX_ExperimentMetrics_EnvironmentId_FlagKey_EventName_Variation~",
                table: "ExperimentMetrics");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ProjectFlagRules");

            migrationBuilder.DropColumn(
                name: "Rollout",
                table: "ProjectFlagRules");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ProjectFeatureFlags");

            migrationBuilder.DropColumn(
                name: "FallthroughRollout",
                table: "FlagEnvironmentStates");

            migrationBuilder.DropColumn(
                name: "OffVariationId",
                table: "FlagEnvironmentStates");

            migrationBuilder.DropColumn(
                name: "VariationId",
                table: "ExperimentMetrics");

            migrationBuilder.DropColumn(
                name: "Rollout",
                table: "ContextualRollouts");

            migrationBuilder.DropColumn(
                name: "VariationId",
                table: "ContextualExperimentMetrics");

            migrationBuilder.DropColumn(
                name: "VariationId",
                table: "AnalyticsExposures");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "ProjectFlagRules",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<int>(
                name: "RolloutPercentage",
                table: "FlagEnvironmentStates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Variant",
                table: "ExperimentMetrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RolloutPercentage",
                table: "ContextualRollouts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Variant",
                table: "ContextualExperimentMetrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Variant",
                table: "AnalyticsExposures",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentMetrics_EnvironmentId_FlagKey_EventName_Variant",
                table: "ExperimentMetrics",
                columns: new[] { "EnvironmentId", "FlagKey", "EventName", "Variant" },
                unique: true);
        }
    }
}
