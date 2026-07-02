using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeFlagMetricBucketsFillfactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "FlagMetricBuckets",
                oldComment: "fillfactor=70");

            migrationBuilder.Sql("ALTER TABLE \"FlagMetricBuckets\" SET (fillfactor = 70);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "FlagMetricBuckets",
                comment: "fillfactor=70");

            migrationBuilder.Sql("ALTER TABLE \"FlagMetricBuckets\" RESET (fillfactor);");
        }
    }
}
