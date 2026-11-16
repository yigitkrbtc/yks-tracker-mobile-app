using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IdentityEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sent_emails",
                schema: "identity",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sent_emails", x => x.event_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sent_emails_sent_at",
                schema: "identity",
                table: "sent_emails",
                column: "sent_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sent_emails",
                schema: "identity");
        }
    }
}
