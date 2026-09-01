using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Problems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "problems");

            migrationBuilder.CreateTable(
                name: "problems",
                schema: "problems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problems", x => x.id);
                    table.CheckConstraint("ck_problems_difficulty", "\"difficulty\" IN ('Easy', 'Medium', 'Hard')");
                });

            migrationBuilder.CreateTable(
                name: "problem_versions",
                schema: "problems",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    problem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    snapshot_reference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    validator_config = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problem_versions", x => x.id);
                    table.CheckConstraint("ck_problem_versions_version_positive", "version >= 1");
                    table.ForeignKey(
                        name: "fk_problem_versions_problems_problem_id",
                        column: x => x.problem_id,
                        principalSchema: "problems",
                        principalTable: "problems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_problem_versions_problem_id_published_at",
                schema: "problems",
                table: "problem_versions",
                columns: new[] { "problem_id", "published_at" },
                filter: "published_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_problem_versions_problem_id_version",
                schema: "problems",
                table: "problem_versions",
                columns: new[] { "problem_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_problems_difficulty",
                schema: "problems",
                table: "problems",
                column: "difficulty");

            migrationBuilder.CreateIndex(
                name: "ix_problems_slug",
                schema: "problems",
                table: "problems",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_problems_tags",
                schema: "problems",
                table: "problems",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "problem_versions",
                schema: "problems");

            migrationBuilder.DropTable(
                name: "problems",
                schema: "problems");
        }
    }
}
