using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Analytics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalysisSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "section_snapshots",
                schema: "analytics",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    exams = table.Column<int>(type: "integer", nullable: false),
                    is_sufficient = table.Column<bool>(type: "boolean", nullable: false),
                    accuracy = table.Column<double>(type: "double precision", nullable: false),
                    trend = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    practice_gap = table.Column<double>(type: "double precision", nullable: true),
                    gap_warning = table.Column<bool>(type: "boolean", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_section_snapshots", x => new { x.user_id, x.section_code });
                });

            migrationBuilder.CreateTable(
                name: "topic_snapshots",
                schema: "analytics",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mastery = table.Column<double>(type: "double precision", nullable: true),
                    mastery_low = table.Column<double>(type: "double precision", nullable: true),
                    mastery_high = table.Column<double>(type: "double precision", nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    confidence_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    band = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trend = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<double>(type: "double precision", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_snapshots", x => new { x.user_id, x.topic_id });
                });

            migrationBuilder.CreateTable(
                name: "user_analyses",
                schema: "analytics",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_stamp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    computed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    invalid_records = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_analyses", x => x.user_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "section_snapshots",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "topic_snapshots",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "user_analyses",
                schema: "analytics");
        }
    }
}
