using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSignOnAttempts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "signon_key_version",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "signon_return",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "signon_state",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "bytea",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<byte[]>(
            name: "signon_verifier",
            schema: "identity",
            table: "preauthentication_sessions",
            type: "bytea",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_preauthentication_sessions_signon",
            schema: "identity",
            table: "preauthentication_sessions",
            sql: "num_nulls(signon_state, signon_verifier, signon_key_version, signon_return) IN (0, 4)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_preauthentication_sessions_signon",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "signon_key_version",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "signon_return",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "signon_state",
            schema: "identity",
            table: "preauthentication_sessions");

        migrationBuilder.DropColumn(
            name: "signon_verifier",
            schema: "identity",
            table: "preauthentication_sessions");
    }
}
