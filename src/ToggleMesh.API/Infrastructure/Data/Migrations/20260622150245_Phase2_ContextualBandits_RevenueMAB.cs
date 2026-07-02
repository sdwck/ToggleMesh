using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_ContextualBandits_RevenueMAB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "ContextPartitionKeys",
                table: "FlagEnvironmentStates",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<int>(
                name: "MabOptimizationType",
                table: "FlagEnvironmentStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "SumOfSquaredValues",
                table: "ExperimentMetrics",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "ContextualExperimentMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlagKey = table.Column<string>(type: "text", nullable: false),
                    EventName = table.Column<string>(type: "text", nullable: false),
                    Variant = table.Column<bool>(type: "boolean", nullable: false),
                    ContextSlice = table.Column<string>(type: "text", nullable: false),
                    TotalExposures = table.Column<long>(type: "bigint", nullable: false),
                    TotalConversions = table.Column<long>(type: "bigint", nullable: false),
                    TotalValue = table.Column<double>(type: "double precision", nullable: false),
                    SumOfSquaredValues = table.Column<double>(type: "double precision", nullable: false),
                    LastCalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContextualExperimentMetrics", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContextualExperimentMetrics");

            migrationBuilder.DropColumn(
                name: "ContextPartitionKeys",
                table: "FlagEnvironmentStates");

            migrationBuilder.DropColumn(
                name: "MabOptimizationType",
                table: "FlagEnvironmentStates");

            migrationBuilder.DropColumn(
                name: "SumOfSquaredValues",
                table: "ExperimentMetrics");
        }
    }
}
