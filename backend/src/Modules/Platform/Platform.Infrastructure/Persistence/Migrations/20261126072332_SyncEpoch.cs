using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nirengi.Modules.Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncEpoch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_epoch",
                schema: "platform",
                columns: table => new
                {
                    epoch = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_epoch", x => x.epoch);
                });

            // Exactly one row: the unique index on a constant refuses a second one. A fresh random epoch per
            // database, so a recreated database is a new history for devices too.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_sync_epoch_single_row ON platform.sync_epoch ((true));");
            migrationBuilder.Sql("INSERT INTO platform.sync_epoch (epoch) VALUES (gen_random_uuid());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_epoch",
                schema: "platform");
        }
    }
}
