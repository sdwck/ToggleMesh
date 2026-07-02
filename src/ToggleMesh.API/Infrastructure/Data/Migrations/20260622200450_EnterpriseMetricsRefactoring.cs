using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnterpriseMetricsRefactoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FalseCount",
                table: "FlagEnvironmentStates");

            migrationBuilder.DropColumn(
                name: "TrueCount",
                table: "FlagEnvironmentStates");

            migrationBuilder.CreateTable(
                name: "FlagMetricBuckets",
                columns: table => new
                {
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlagKey = table.Column<string>(type: "text", nullable: false),
                    TimestampBucket = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrueCount = table.Column<long>(type: "bigint", nullable: false),
                    FalseCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlagMetricBuckets", x => new { x.EnvironmentId, x.FlagKey, x.TimestampBucket });
                    table.ForeignKey(
                        name: "FK_FlagMetricBuckets_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "fillfactor=70");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlagMetricBuckets");

            migrationBuilder.AddColumn<long>(
                name: "FalseCount",
                table: "FlagEnvironmentStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TrueCount",
                table: "FlagEnvironmentStates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
