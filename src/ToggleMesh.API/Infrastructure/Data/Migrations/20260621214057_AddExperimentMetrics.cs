using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExperimentMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExperimentMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlagKey = table.Column<string>(type: "text", nullable: false),
                    EventName = table.Column<string>(type: "text", nullable: false),
                    Variant = table.Column<bool>(type: "boolean", nullable: false),
                    TotalExposures = table.Column<long>(type: "bigint", nullable: false),
                    TotalConversions = table.Column<long>(type: "bigint", nullable: false),
                    TotalValue = table.Column<double>(type: "double precision", nullable: false),
                    LastCalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentMetrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentMetrics_EnvironmentId_FlagKey_EventName_Variant",
                table: "ExperimentMetrics",
                columns: new[] { "EnvironmentId", "FlagKey", "EventName", "Variant" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExperimentMetrics");
        }
    }
}
