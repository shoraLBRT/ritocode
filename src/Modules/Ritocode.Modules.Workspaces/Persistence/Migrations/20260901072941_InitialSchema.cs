using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Workspaces.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workspaces");

            migrationBuilder.CreateTable(
                name: "workspaces",
                schema: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    problem_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_reference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspaces", x => x.id);
                    table.CheckConstraint("ck_workspaces_updated_not_before_created", "updated_at >= created_at");
                });

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_problem_version_id",
                schema: "workspaces",
                table: "workspaces",
                column: "problem_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_user_id_problem_version_id",
                schema: "workspaces",
                table: "workspaces",
                columns: new[] { "user_id", "problem_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_user_id_updated_at",
                schema: "workspaces",
                table: "workspaces",
                columns: new[] { "user_id", "updated_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspaces",
                schema: "workspaces");
        }
    }
}
