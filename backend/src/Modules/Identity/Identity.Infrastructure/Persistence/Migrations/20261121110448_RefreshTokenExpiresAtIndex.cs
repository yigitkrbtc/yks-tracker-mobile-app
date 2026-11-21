using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefreshTokenExpiresAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                schema: "identity",
                table: "refresh_tokens",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_expires_at",
                schema: "identity",
                table: "refresh_tokens");
        }
    }
}
