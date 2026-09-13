using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Submissions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "started_at",
                schema: "submissions",
                table: "submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_submissions_started_at_matches_status",
                schema: "submissions",
                table: "submissions",
                sql: "status = 'Failed' OR ((status = 'Queued') = (started_at IS NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_submissions_started_at_matches_status",
                schema: "submissions",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "submissions",
                table: "submissions");
        }
    }
}
