using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exam_tests",
                schema: "curriculum",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    session = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    question_count = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exam_tests", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "releases",
                schema: "curriculum",
                columns: table => new
                {
                    exam_year = table.Column<int>(type: "integer", nullable: false),
                    seed_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    seeded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_releases", x => x.exam_year);
                });

            migrationBuilder.CreateTable(
                name: "score_types",
                schema: "curriculum",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    test_codes = table.Column<List<string>>(type: "text[]", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_types", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "subjects",
                schema: "curriculum",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    session = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    test_codes = table.Column<List<string>>(type: "text[]", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subjects", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "topics",
                schema: "curriculum",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subtopics = table.Column<List<string>>(type: "text[]", nullable: false),
                    grade_min = table.Column<int>(type: "integer", nullable: false),
                    grade_max = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "topic_weights",
                schema: "curriculum",
                columns: table => new
                {
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_year = table.Column<int>(type: "integer", nullable: false),
                    expected_questions = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    years_asked = table.Column<int>(type: "integer", nullable: false),
                    years_counted = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_weights", x => new { x.topic_id, x.exam_year });
                    table.ForeignKey(
                        name: "fk_topic_weights_topics_topic_id",
                        column: x => x.topic_id,
                        principalSchema: "curriculum",
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_topics_code",
                schema: "curriculum",
                table: "topics",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topics_subject_code",
                schema: "curriculum",
                table: "topics",
                column: "subject_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exam_tests",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "releases",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "score_types",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "subjects",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "topic_weights",
                schema: "curriculum");

            migrationBuilder.DropTable(
                name: "topics",
                schema: "curriculum");
        }
    }
}
