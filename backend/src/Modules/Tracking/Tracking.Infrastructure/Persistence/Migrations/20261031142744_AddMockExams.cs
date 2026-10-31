using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMockExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mock_exams",
                schema: "tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    taken_on = table.Column<DateOnly>(type: "date", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    client_modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    change_seq = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mock_exams", x => x.id);
                    table.CheckConstraint("ck_mock_exams_source", "deleted_at IS NOT NULL OR (source_id IS NULL) <> (source_name IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_sections",
                schema: "tracking",
                columns: table => new
                {
                    mock_exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taken_on = table.Column<DateOnly>(type: "date", nullable: false),
                    question_count = table.Column<int>(type: "integer", nullable: false),
                    correct = table.Column<int>(type: "integer", nullable: false),
                    wrong = table.Column<int>(type: "integer", nullable: false),
                    blank = table.Column<int>(type: "integer", nullable: false),
                    net = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    tagging_status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mock_exam_sections", x => new { x.mock_exam_id, x.section_code });
                    table.CheckConstraint("ck_mock_exam_sections_counts", "correct >= 0 AND wrong >= 0 AND blank >= 0 AND correct + wrong + blank = question_count AND question_count BETWEEN 1 AND 80");
                    table.ForeignKey(
                        name: "fk_mock_exam_sections_mock_exams_mock_exam_id",
                        column: x => x.mock_exam_id,
                        principalSchema: "tracking",
                        principalTable: "mock_exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mock_exam_topic_marks",
                schema: "tracking",
                columns: table => new
                {
                    mock_exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    taken_on = table.Column<DateOnly>(type: "date", nullable: false),
                    wrong = table.Column<int>(type: "integer", nullable: false),
                    blank = table.Column<int>(type: "integer", nullable: false),
                    mistake_knowledge = table.Column<int>(type: "integer", nullable: true),
                    mistake_attention = table.Column<int>(type: "integer", nullable: true),
                    mistake_time = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mock_exam_topic_marks", x => new { x.mock_exam_id, x.section_code, x.topic_id });
                    table.CheckConstraint("ck_mock_exam_topic_marks_counts", "wrong >= 0 AND blank >= 0 AND wrong + blank >= 1");
                    table.ForeignKey(
                        name: "fk_mock_exam_topic_marks_mock_exams_mock_exam_id",
                        column: x => x.mock_exam_id,
                        principalSchema: "tracking",
                        principalTable: "mock_exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_sections_user_id_section_code_taken_on",
                schema: "tracking",
                table: "mock_exam_sections",
                columns: new[] { "user_id", "section_code", "taken_on" });

            migrationBuilder.CreateIndex(
                name: "ix_mock_exam_topic_marks_user_id_topic_id_taken_on",
                schema: "tracking",
                table: "mock_exam_topic_marks",
                columns: new[] { "user_id", "topic_id", "taken_on" });

            migrationBuilder.CreateIndex(
                name: "ix_mock_exams_user_id_change_seq",
                schema: "tracking",
                table: "mock_exams",
                columns: new[] { "user_id", "change_seq" });

            migrationBuilder.CreateIndex(
                name: "ix_mock_exams_user_id_taken_on",
                schema: "tracking",
                table: "mock_exams",
                columns: new[] { "user_id", "taken_on" },
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mock_exam_sections",
                schema: "tracking");

            migrationBuilder.DropTable(
                name: "mock_exam_topic_marks",
                schema: "tracking");

            migrationBuilder.DropTable(
                name: "mock_exams",
                schema: "tracking");
        }
    }
}
