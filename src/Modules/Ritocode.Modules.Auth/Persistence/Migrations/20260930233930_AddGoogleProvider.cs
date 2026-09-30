using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Auth.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_linked_accounts_provider",
                schema: "auth",
                table: "linked_accounts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_linked_accounts_provider",
                schema: "auth",
                table: "linked_accounts",
                sql: "\"provider\" IN ('GitHub', 'Google')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_linked_accounts_provider",
                schema: "auth",
                table: "linked_accounts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_linked_accounts_provider",
                schema: "auth",
                table: "linked_accounts",
                sql: "\"provider\" IN ('GitHub')");
        }
    }
}
