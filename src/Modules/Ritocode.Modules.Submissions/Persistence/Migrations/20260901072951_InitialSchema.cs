using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Submissions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "submissions");

            migrationBuilder.CreateTable(
                name: "submissions",
                schema: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_completed_at_matches_status", "(status IN ('Completed', 'Failed')) = (completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_submissions_score_range", "score IS NULL OR (score >= 0 AND score <= 100)");
                    table.CheckConstraint("ck_submissions_status", "\"status\" IN ('Queued', 'Running', 'Completed', 'Failed')");
                });

            migrationBuilder.CreateTable(
                name: "submission_reports",
                schema: "submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    validator_results = table.Column<string>(type: "jsonb", nullable: false),
                    logs_reference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_reports_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "submissions",
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_submission_reports_submission_id",
                schema: "submissions",
                table: "submission_reports",
                column: "submission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_status_created_at",
                schema: "submissions",
                table: "submissions",
                columns: new[] { "status", "created_at" },
                filter: "status IN ('Queued', 'Running')");

            migrationBuilder.CreateIndex(
                name: "ix_submissions_user_id_created_at",
                schema: "submissions",
                table: "submissions",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_workspace_id",
                schema: "submissions",
                table: "submissions",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_reports",
                schema: "submissions");

            migrationBuilder.DropTable(
                name: "submissions",
                schema: "submissions");
        }
    }
}
