using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTopicTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "change_seq",
                schema: "tracking");

            migrationBuilder.CreateTable(
                name: "topic_tests",
                schema: "tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    studied_on = table.Column<DateOnly>(type: "date", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_repeat = table.Column<bool>(type: "boolean", nullable: false),
                    difficulty = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
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
                    table.PrimaryKey("pk_topic_tests", x => x.id);
                    table.CheckConstraint("ck_topic_tests_source", "deleted_at IS NOT NULL OR (source_id IS NULL) <> (source_name IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "topic_test_results",
                schema: "tracking",
                columns: table => new
                {
                    topic_test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    studied_on = table.Column<DateOnly>(type: "date", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    correct = table.Column<int>(type: "integer", nullable: false),
                    wrong = table.Column<int>(type: "integer", nullable: false),
                    blank = table.Column<int>(type: "integer", nullable: false),
                    net = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    mistake_knowledge = table.Column<int>(type: "integer", nullable: true),
                    mistake_attention = table.Column<int>(type: "integer", nullable: true),
                    mistake_time = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topic_test_results", x => new { x.topic_test_id, x.topic_id });
                    table.CheckConstraint("ck_topic_test_results_counts", "correct >= 0 AND wrong >= 0 AND blank >= 0 AND correct + wrong + blank = total AND total BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "fk_topic_test_results_topic_tests_topic_test_id",
                        column: x => x.topic_test_id,
                        principalSchema: "tracking",
                        principalTable: "topic_tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_topic_test_results_user_id_topic_id_studied_on",
                schema: "tracking",
                table: "topic_test_results",
                columns: new[] { "user_id", "topic_id", "studied_on" });

            migrationBuilder.CreateIndex(
                name: "ix_topic_tests_user_id_change_seq",
                schema: "tracking",
                table: "topic_tests",
                columns: new[] { "user_id", "change_seq" });

            migrationBuilder.CreateIndex(
                name: "ix_topic_tests_user_id_studied_on",
                schema: "tracking",
                table: "topic_tests",
                columns: new[] { "user_id", "studied_on" },
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "topic_test_results",
                schema: "tracking");

            migrationBuilder.DropTable(
                name: "topic_tests",
                schema: "tracking");

            migrationBuilder.DropSequence(
                name: "change_seq",
                schema: "tracking");
        }
    }
}
