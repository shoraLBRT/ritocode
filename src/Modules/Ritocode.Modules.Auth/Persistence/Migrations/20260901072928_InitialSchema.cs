using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ritocode.Modules.Auth.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.CreateTable(
                name: "linked_accounts",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_user_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_login = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_linked_accounts", x => x.id);
                    table.CheckConstraint("ck_linked_accounts_provider", "\"provider\" IN ('GitHub')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_linked_accounts_provider_provider_user_id",
                schema: "auth",
                table: "linked_accounts",
                columns: new[] { "provider", "provider_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_linked_accounts_user_id_provider",
                schema: "auth",
                table: "linked_accounts",
                columns: new[] { "user_id", "provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "linked_accounts",
                schema: "auth");
        }
    }
}
