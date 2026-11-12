using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Attempts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReveals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reveals",
                schema: "attempts",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    revealed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reveals", x => new { x.user_id, x.task_slug });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reveals",
                schema: "attempts");
        }
    }
}
