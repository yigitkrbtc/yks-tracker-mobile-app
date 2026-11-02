using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WeightsPerTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows get test_code '' and are replaced by the seeder (parser version 4 changes the seed hash).
            migrationBuilder.DropPrimaryKey(
                name: "pk_topic_weights",
                schema: "curriculum",
                table: "topic_weights");

            migrationBuilder.AddColumn<string>(
                name: "test_code",
                schema: "curriculum",
                table: "topic_weights",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "pk_topic_weights",
                schema: "curriculum",
                table: "topic_weights",
                columns: new[] { "topic_id", "exam_year", "test_code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Old key is one row per topic: keep one row of the two-test topics (the next seed run rewrites them).
            migrationBuilder.Sql(
                "DELETE FROM curriculum.topic_weights a USING curriculum.topic_weights b "
                + "WHERE a.topic_id = b.topic_id AND a.exam_year = b.exam_year AND a.test_code > b.test_code;");

            migrationBuilder.DropPrimaryKey(
                name: "pk_topic_weights",
                schema: "curriculum",
                table: "topic_weights");

            migrationBuilder.DropColumn(
                name: "test_code",
                schema: "curriculum",
                table: "topic_weights");

            migrationBuilder.AddPrimaryKey(
                name: "pk_topic_weights",
                schema: "curriculum",
                table: "topic_weights",
                columns: new[] { "topic_id", "exam_year" });
        }
    }
}
