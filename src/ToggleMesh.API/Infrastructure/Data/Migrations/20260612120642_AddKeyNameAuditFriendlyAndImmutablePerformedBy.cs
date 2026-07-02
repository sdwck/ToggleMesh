using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKeyNameAuditFriendlyAndImmutablePerformedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PerformedBy",
                table: "AuditLogs",
                newName: "PerformedByEmail");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "EnvironmentKeys",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EntityFriendlyName",
                table: "AuditLogs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PerformedById",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_PerformedById",
                table: "AuditLogs",
                column: "PerformedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_PerformedById",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "EnvironmentKeys");

            migrationBuilder.DropColumn(
                name: "EntityFriendlyName",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PerformedById",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "PerformedByEmail",
                table: "AuditLogs",
                newName: "PerformedBy");
        }
    }
}
