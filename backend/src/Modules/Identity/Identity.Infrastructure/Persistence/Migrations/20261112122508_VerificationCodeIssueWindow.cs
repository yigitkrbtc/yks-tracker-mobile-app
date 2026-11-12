using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerificationCodeIssueWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "issue_window_started_at",
                schema: "identity",
                table: "verification_codes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "issues_in_window",
                schema: "identity",
                table: "verification_codes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "issue_window_started_at",
                schema: "identity",
                table: "verification_codes");

            migrationBuilder.DropColumn(
                name: "issues_in_window",
                schema: "identity",
                table: "verification_codes");
        }
    }
}
