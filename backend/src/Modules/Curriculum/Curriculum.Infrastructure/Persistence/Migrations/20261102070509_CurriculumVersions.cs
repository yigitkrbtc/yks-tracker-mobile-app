using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CurriculumVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                schema: "curriculum",
                table: "releases",
                type: "character(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "version_number",
                schema: "curriculum",
                table: "releases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Until version 1 is backfilled at startup the live state is identified by the seed alone.
            migrationBuilder.Sql("UPDATE curriculum.releases SET content_hash = seed_hash;");

            migrationBuilder.CreateTable(
                name: "curriculum_drafts",
                schema: "curriculum",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_year = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    based_on_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    operations = table.Column<string>(type: "jsonb", nullable: false),
                    seed_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curriculum_drafts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_versions",
                schema: "curriculum",
                columns: table => new
                {
                    exam_year = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    seed_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    operations = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curriculum_versions", x => new { x.exam_year, x.number });
                });

            migrationBuilder.CreateTable(
                name: "exam_calendars",
                schema: "curriculum",
                columns: table => new
                {
                    exam_year = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_calendars", x => x.exam_year);
                });

            migrationBuilder.CreateTable(
                name: "topic_lineage",
                schema: "curriculum",
                columns: table => new
                {
                    from_topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    exam_year = table.Column<int>(type: "integer", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_lineage", x => new { x.from_topic_id, x.to_topic_id });
                    table.ForeignKey(
                        name: "fk_topic_lineage_topics_from_topic_id",
                        column: x => x.from_topic_id,
                        principalSchema: "curriculum",
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_topic_lineage_topics_to_topic_id",
                        column: x => x.to_topic_id,
                        principalSchema: "curriculum",
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_drafts_exam_year_kind",
                schema: "curriculum",
                table: "curriculum_drafts",
                columns: new[] { "exam_year", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topic_lineage_to_topic_id",
                schema: "curriculum",
                table: "topic_lineage",
                column: "to_topic_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curriculum_drafts",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "curriculum_versions",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "exam_calendars",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "topic_lineage",
                schema: "curriculum");

            migrationBuilder.DropColumn(
                name: "content_hash",
                schema: "curriculum",
                table: "releases");

            migrationBuilder.DropColumn(
                name: "version_number",
                schema: "curriculum",
                table: "releases");
        }
    }
}
