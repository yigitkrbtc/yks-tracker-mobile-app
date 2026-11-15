using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerificationCodeResendBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "codes_in_window",
                schema: "identity",
                table: "verification_codes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "window_started_at",
                schema: "identity",
                table: "verification_codes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Live codes count as the first of a window that began when they were sent.
            migrationBuilder.Sql("UPDATE identity.verification_codes SET window_started_at = sent_at, codes_in_window = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "codes_in_window",
                schema: "identity",
                table: "verification_codes");

            migrationBuilder.DropColumn(
                name: "window_started_at",
                schema: "identity",
                table: "verification_codes");
        }
    }
}
