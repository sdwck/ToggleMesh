using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToggleMesh.API.Migrations
{
    /// <inheritdoc />
    public partial class HashApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ApiKey",
                table: "EnvironmentKeys",
                newName: "KeyHash");

            migrationBuilder.RenameIndex(
                name: "IX_EnvironmentKeys_ApiKey",
                table: "EnvironmentKeys",
                newName: "IX_EnvironmentKeys_KeyHash");

            migrationBuilder.AddColumn<string>(
                name: "KeyPreview",
                table: "EnvironmentKeys",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyPreview",
                table: "EnvironmentKeys");

            migrationBuilder.RenameColumn(
                name: "KeyHash",
                table: "EnvironmentKeys",
                newName: "ApiKey");

            migrationBuilder.RenameIndex(
                name: "IX_EnvironmentKeys_KeyHash",
                table: "EnvironmentKeys",
                newName: "IX_EnvironmentKeys_ApiKey");
        }
    }
}
