using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Errors.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "errors");

            migrationBuilder.CreateTable(
                name: "error_groups",
                schema: "errors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    count = table.Column<long>(type: "bigint", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_groups", x => x.id);
                    table.CheckConstraint("ck_error_groups_source", "\"source\" IN ('Server', 'Browser')");
                });

            migrationBuilder.CreateTable(
                name: "error_occurrences",
                schema: "errors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    stack = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    commit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_error_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "fk_error_occurrences_error_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "errors",
                        principalTable: "error_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_error_groups_resolved_at_last_seen_at",
                schema: "errors",
                table: "error_groups",
                columns: new[] { "resolved_at", "last_seen_at" });

            migrationBuilder.CreateIndex(
                name: "ux_error_groups_fingerprint",
                schema: "errors",
                table: "error_groups",
                column: "fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_error_occurrences_group_id_occurred_at",
                schema: "errors",
                table: "error_occurrences",
                columns: new[] { "group_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_error_occurrences_occurred_at",
                schema: "errors",
                table: "error_occurrences",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "error_occurrences",
                schema: "errors");

            migrationBuilder.DropTable(
                name: "error_groups",
                schema: "errors");
        }
    }
}
