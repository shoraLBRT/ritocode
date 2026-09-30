using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Users.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropXpAndTrustLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_trust_level",
                schema: "users",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_xp_not_negative",
                schema: "users",
                table: "users");

            migrationBuilder.DropColumn(
                name: "trust_level",
                schema: "users",
                table: "users");

            migrationBuilder.DropColumn(
                name: "xp",
                schema: "users",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "trust_level",
                schema: "users",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "xp",
                schema: "users",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_trust_level",
                schema: "users",
                table: "users",
                sql: "\"trust_level\" IN ('New', 'Established', 'Trusted')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_xp_not_negative",
                schema: "users",
                table: "users",
                sql: "xp >= 0");
        }
    }
}
