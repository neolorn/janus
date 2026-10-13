using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddChallengeIdentifiers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<int>(
            name: "fingerprint_version",
            schema: "identity",
            table: "signin_challenges",
            type: "integer",
            nullable: false);

        migrationBuilder.AddColumn<byte[]>(
            name: "identifier",
            schema: "identity",
            table: "signin_challenges",
            type: "bytea",
            maxLength: 32,
            nullable: false);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signin_challenges_identifier",
            schema: "identity",
            table: "signin_challenges",
            sql: "octet_length(identifier) = 32");

        // OPS-SEC-003 AC6: the identifier's hash stands behind no value the library
        // holds, so the rotation reads the version it was computed under and forgets
        // the sign-in once the version is retired, and never reads the hash (entry 318
        // of the decisions pending review).
        migrationBuilder.Sql(
            """
            GRANT SELECT (fingerprint_version), DELETE ON identity.signin_challenges TO identity_maintenance;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            REVOKE SELECT (fingerprint_version), DELETE ON identity.signin_challenges FROM identity_maintenance;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_signin_challenges_identifier",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropColumn(
            name: "fingerprint_version",
            schema: "identity",
            table: "signin_challenges");

        migrationBuilder.DropColumn(
            name: "identifier",
            schema: "identity",
            table: "signin_challenges");
    }
}
