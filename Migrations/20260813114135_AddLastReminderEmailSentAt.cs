using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadScoutCRM.Migrations
{
    /// <inheritdoc />
    public partial class AddLastReminderEmailSentAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderEmailSentAt",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReminderEmailSentAt",
                table: "AspNetUsers");
        }
    }
}
