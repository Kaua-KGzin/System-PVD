using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Archlab.Backend.Migrations
{
    /// <inheritdoc />
    public partial class Semana4_Security : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Invalidate all existing refresh tokens before schema change.
            // Old tokens were stored as plain text; new tokens are SHA-256 hashes.
            // Users will need to log in again after this migration.
            migrationBuilder.Sql("UPDATE \"RefreshTokens\" SET \"IsRevoked\" = true WHERE \"IsRevoked\" = false;");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByToken",
                table: "RefreshTokens",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TokenFamily",
                table: "RefreshTokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "FiscalCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalCounters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenFamily",
                table: "RefreshTokens",
                column: "TokenFamily");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalCounters");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_TokenFamily",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByToken",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "TokenFamily",
                table: "RefreshTokens");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
