using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlagEnvironmentStateRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FlagMetricBuckets",
                table: "FlagMetricBuckets");

            migrationBuilder.DropColumn(
                name: "FalseCount",
                table: "FlagMetricBuckets");

            migrationBuilder.RenameColumn(
                name: "TrueCount",
                table: "FlagMetricBuckets",
                newName: "Count");

            migrationBuilder.AddColumn<Guid>(
                name: "VariationId",
                table: "FlagMetricBuckets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "FlagEnvironmentStates",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlagMetricBuckets",
                table: "FlagMetricBuckets",
                columns: new[] { "EnvironmentId", "FlagKey", "TimestampBucket", "VariationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_FlagMetricBuckets",
                table: "FlagMetricBuckets");

            migrationBuilder.DropColumn(
                name: "VariationId",
                table: "FlagMetricBuckets");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "FlagEnvironmentStates");

            migrationBuilder.RenameColumn(
                name: "Count",
                table: "FlagMetricBuckets",
                newName: "TrueCount");

            migrationBuilder.AddColumn<long>(
                name: "FalseCount",
                table: "FlagMetricBuckets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddPrimaryKey(
                name: "PK_FlagMetricBuckets",
                table: "FlagMetricBuckets",
                columns: new[] { "EnvironmentId", "FlagKey", "TimestampBucket" });
        }
    }
}
