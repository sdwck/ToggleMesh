using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewedByToPendingFlagChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "PendingFlagChanges",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingFlagChanges_ReviewedByUserId",
                table: "PendingFlagChanges",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PendingFlagChanges_AspNetUsers_ReviewedByUserId",
                table: "PendingFlagChanges",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PendingFlagChanges_AspNetUsers_ReviewedByUserId",
                table: "PendingFlagChanges");

            migrationBuilder.DropIndex(
                name: "IX_PendingFlagChanges_ReviewedByUserId",
                table: "PendingFlagChanges");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "PendingFlagChanges");
        }
    }
}
