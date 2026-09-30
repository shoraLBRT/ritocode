using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Content.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.CreateTable(
                name: "cards",
                schema: "content",
                columns: table => new
                {
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    @class = table.Column<string>(name: "class", type: "character varying(64)", maxLength: 64, nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    texts = table.Column<string>(type: "jsonb", nullable: false),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    content_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cards", x => x.slug);
                    table.CheckConstraint("ck_cards_weight_range", "weight BETWEEN 1 AND 3");
                });

            migrationBuilder.CreateTable(
                name: "materials",
                schema: "content",
                columns: table => new
                {
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    language = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    files = table.Column<string>(type: "jsonb", nullable: false),
                    overview = table.Column<string>(type: "jsonb", nullable: false),
                    content_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materials", x => x.slug);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                schema: "content",
                columns: table => new
                {
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    material = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    findings = table.Column<string>(type: "jsonb", nullable: false),
                    texts = table.Column<string>(type: "jsonb", nullable: false),
                    shortlist = table.Column<string[]>(type: "text[]", nullable: false),
                    unpublished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    content_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.slug);
                    table.CheckConstraint("ck_tasks_difficulty", "difficulty IN ('easy', 'medium', 'hard')");
                });

            migrationBuilder.CreateTable(
                name: "taxonomy",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    document = table.Column<string>(type: "jsonb", nullable: false),
                    content_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_taxonomy", x => x.id);
                    table.CheckConstraint("ck_taxonomy_singleton", "id = 1");
                });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_material",
                schema: "content",
                table: "tasks",
                column: "material");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cards",
                schema: "content");

            migrationBuilder.DropTable(
                name: "materials",
                schema: "content");

            migrationBuilder.DropTable(
                name: "tasks",
                schema: "content");

            migrationBuilder.DropTable(
                name: "taxonomy",
                schema: "content");
        }
    }
}
