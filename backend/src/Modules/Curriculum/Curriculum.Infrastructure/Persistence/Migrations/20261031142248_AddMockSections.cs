using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMockSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mock_sections",
                schema: "curriculum",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    test_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    question_count = table.Column<int>(type: "integer", nullable: false),
                    parent_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    alternative_of = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mock_sections", x => x.code);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mock_sections",
                schema: "curriculum");
        }
    }
}
