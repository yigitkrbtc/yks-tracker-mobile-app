using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerificationCodeAttemptWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "attempt_window_started_at",
                schema: "identity",
                table: "verification_codes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "failed_attempts_in_window",
                schema: "identity",
                table: "verification_codes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "issued_with_mfa",
                schema: "identity",
                table: "refresh_tokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attempt_window_started_at",
                schema: "identity",
                table: "verification_codes");

            migrationBuilder.DropColumn(
                name: "failed_attempts_in_window",
                schema: "identity",
                table: "verification_codes");

            migrationBuilder.DropColumn(
                name: "issued_with_mfa",
                schema: "identity",
                table: "refresh_tokens");
        }
    }
}
