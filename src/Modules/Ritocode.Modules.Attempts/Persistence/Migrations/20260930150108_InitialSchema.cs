using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Attempts.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "attempts");

            migrationBuilder.CreateTable(
                name: "attempts",
                schema: "attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    step = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    content_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    answer = table.Column<string>(type: "jsonb", nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    score = table.Column<int>(type: "integer", nullable: true),
                    max_score = table.Column<int>(type: "integer", nullable: true),
                    counts_toward_progress = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attempts", x => x.id);
                    table.CheckConstraint("ck_attempts_counts_only_when_submitted", "NOT counts_toward_progress OR submitted_at IS NOT NULL");
                    table.CheckConstraint("ck_attempts_score_range", "score IS NULL OR (score >= 0 AND score <= max_score)");
                    table.CheckConstraint("ck_attempts_step", "\"step\" IN ('Diagnosis', 'Treatment')");
                    table.CheckConstraint("ck_attempts_submitted_whole", "(submitted_at IS NULL) = (content_revision IS NULL) AND (submitted_at IS NULL) = (answer IS NULL) AND (submitted_at IS NULL) = (result IS NULL) AND (submitted_at IS NULL) = (score IS NULL) AND (submitted_at IS NULL) = (max_score IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_attempts_user_id_started_at",
                schema: "attempts",
                table: "attempts",
                columns: new[] { "user_id", "started_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_attempts_user_id_submitted_at",
                schema: "attempts",
                table: "attempts",
                columns: new[] { "user_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_attempts_user_id_task_slug",
                schema: "attempts",
                table: "attempts",
                columns: new[] { "user_id", "task_slug" });

            migrationBuilder.CreateIndex(
                name: "ux_attempts_first_submission",
                schema: "attempts",
                table: "attempts",
                columns: new[] { "user_id", "task_slug" },
                unique: true,
                filter: "counts_toward_progress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attempts",
                schema: "attempts");
        }
    }
}
