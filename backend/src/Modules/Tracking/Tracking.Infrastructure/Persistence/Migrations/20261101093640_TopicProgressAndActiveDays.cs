using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TopicProgressAndActiveDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "weekly_active_days_goal",
                schema: "tracking",
                table: "study_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "topic_progress",
                schema: "tracking",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    client_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    change_seq = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_progress", x => new { x.user_id, x.topic_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_topic_progress_user_id_change_seq",
                schema: "tracking",
                table: "topic_progress",
                columns: new[] { "user_id", "change_seq" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topic_progress",
                schema: "tracking");

            migrationBuilder.DropColumn(
                name: "weekly_active_days_goal",
                schema: "tracking",
                table: "study_profiles");
        }
    }
}
