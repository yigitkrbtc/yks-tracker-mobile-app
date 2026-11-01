using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Recommendation.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "item_feedback",
                schema: "recommendation",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    item_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_feedback", x => new { x.user_id, x.week_start, x.item_key });
                });

            migrationBuilder.CreateTable(
                name: "weekly_plans",
                schema: "recommendation",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    rule_set_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    analysis_stamp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    items = table.Column<string>(type: "jsonb", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_plans", x => new { x.user_id, x.week_start });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_feedback",
                schema: "recommendation");

            migrationBuilder.DropTable(
                name: "weekly_plans",
                schema: "recommendation");
        }
    }
}
