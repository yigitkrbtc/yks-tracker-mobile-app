using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OutboxRetryBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_at",
                schema: "platform",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "platform",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_at",
                schema: "platform",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "platform",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");
        }
    }
}
