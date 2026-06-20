using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Webhooks");

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "Webhooks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Webhooks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WebhookId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookDeliveries_Webhooks_WebhookId",
                        column: x => x.WebhookId,
                        principalTable: "Webhooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAt",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_WebhookId",
                table: "WebhookDeliveries",
                column: "WebhookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "Webhooks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Webhooks");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Webhooks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
