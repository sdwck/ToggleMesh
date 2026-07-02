using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsPartitioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE ""AnalyticsExposures"" (
                    ""Id"" uuid NOT NULL,
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    ""EnvironmentId"" uuid NOT NULL,
                    ""FlagKey"" text NOT NULL,
                    ""Identity"" text NOT NULL,
                    ""Variant"" boolean NOT NULL,
                    CONSTRAINT ""PK_AnalyticsExposures"" PRIMARY KEY (""Id"", ""Timestamp"")
                ) PARTITION BY RANGE (""Timestamp"");
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE ""AnalyticsTracks"" (
                    ""Id"" uuid NOT NULL,
                    ""Timestamp"" timestamp with time zone NOT NULL,
                    ""EnvironmentId"" uuid NOT NULL,
                    ""Identity"" text NOT NULL,
                    ""EventName"" text NOT NULL,
                    ""Value"" double precision NULL,
                    ""Properties"" jsonb NULL,
                    CONSTRAINT ""PK_AnalyticsTracks"" PRIMARY KEY (""Id"", ""Timestamp"")
                ) PARTITION BY RANGE (""Timestamp"");
            ");

            migrationBuilder.Sql(@"CREATE TABLE ""AnalyticsExposures_Default"" PARTITION OF ""AnalyticsExposures"" DEFAULT;");
            migrationBuilder.Sql(@"CREATE TABLE ""AnalyticsTracks_Default"" PARTITION OF ""AnalyticsTracks"" DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsExposures_EnvironmentId",
                table: "AnalyticsExposures",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsExposures_EnvironmentId_Identity_Timestamp",
                table: "AnalyticsExposures",
                columns: new[] { "EnvironmentId", "Identity", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsExposures_FlagKey",
                table: "AnalyticsExposures",
                column: "FlagKey");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsExposures_Timestamp",
                table: "AnalyticsExposures",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsTracks_EnvironmentId",
                table: "AnalyticsTracks",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsTracks_EnvironmentId_Identity_Timestamp",
                table: "AnalyticsTracks",
                columns: new[] { "EnvironmentId", "Identity", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsTracks_EventName",
                table: "AnalyticsTracks",
                column: "EventName");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsTracks_Timestamp",
                table: "AnalyticsTracks",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsExposures");

            migrationBuilder.DropTable(
                name: "AnalyticsTracks");
        }
    }
}
