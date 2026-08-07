using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadScoutCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApiKeyCreatedAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiKeyHash",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApiKeyLastUsedAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ApiKeyHash",
                table: "AspNetUsers",
                column: "ApiKeyHash",
                unique: true,
                filter: "[ApiKeyHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ApiKeyHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApiKeyCreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApiKeyHash",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ApiKeyLastUsedAt",
                table: "AspNetUsers");
        }
    }
}
