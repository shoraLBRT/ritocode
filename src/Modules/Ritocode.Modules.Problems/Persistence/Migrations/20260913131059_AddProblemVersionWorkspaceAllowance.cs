using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Problems.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemVersionWorkspaceAllowance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rows written before these columns existed get no editable files — a version that never
            // said what may change refuses every save rather than guessing — and the manifest format's
            // default limits, which the check constraint below accepts. The model carries no default,
            // so a new row always states its own. A version ingested before this migration is re-ingested
            // to become editable; see docs/PROJECT_STATE.md.
            migrationBuilder.AddColumn<string[]>(
                name: "editable_files",
                schema: "problems",
                table: "problem_versions",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<int>(
                name: "max_file_bytes",
                schema: "problems",
                table: "problem_versions",
                type: "integer",
                nullable: false,
                defaultValue: 262144);

            migrationBuilder.AddColumn<int>(
                name: "max_files",
                schema: "problems",
                table: "problem_versions",
                type: "integer",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<int>(
                name: "max_total_bytes",
                schema: "problems",
                table: "problem_versions",
                type: "integer",
                nullable: false,
                defaultValue: 5242880);

            migrationBuilder.AddCheckConstraint(
                name: "ck_problem_versions_limits_valid",
                schema: "problems",
                table: "problem_versions",
                sql: "max_files >= 1 AND max_file_bytes >= 1 AND max_total_bytes >= max_file_bytes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_problem_versions_limits_valid",
                schema: "problems",
                table: "problem_versions");

            migrationBuilder.DropColumn(
                name: "editable_files",
                schema: "problems",
                table: "problem_versions");

            migrationBuilder.DropColumn(
                name: "max_file_bytes",
                schema: "problems",
                table: "problem_versions");

            migrationBuilder.DropColumn(
                name: "max_files",
                schema: "problems",
                table: "problem_versions");

            migrationBuilder.DropColumn(
                name: "max_total_bytes",
                schema: "problems",
                table: "problem_versions");
        }
    }
}
