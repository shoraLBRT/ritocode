using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Submissions.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionInputReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No default, deliberately, unlike what `migrations add` generates. An empty string is not a
            // storage reference, and a column defaulting to one would let an insert that forgot the
            // frozen tree succeed and fail later, in the worker. Nothing has ever written a submission,
            // so there is no existing row the absent default could strand.
            migrationBuilder.AddColumn<string>(
                name: "input_reference",
                schema: "submissions",
                table: "submissions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "input_reference",
                schema: "submissions",
                table: "submissions");
        }
    }
}
