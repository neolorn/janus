using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HoldClientSecretsWrapped : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // OPS-SEC-001, OPS-SEC-002, D-166 (340): the registry holds each secret wrapped
        // under the deployment's data key and when it was drawn. A row holds what its
        // secret hashes to, from which no secret can be had again, so the migration runs
        // only where no client is registered.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.oidc_clients) THEN
                    RAISE EXCEPTION 'Clients are registered with secrets held as what they hash to.';
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "secret_issued_at",
            schema: "identity",
            table: "oidc_clients",
            type: "timestamp with time zone",
            nullable: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // A wrapped secret is not what the earlier registry compared a presented one
        // with, so the column goes only where no client is registered.
        migrationBuilder.Sql(
            """
            DO $do$
            BEGIN
                IF EXISTS (SELECT FROM identity.oidc_clients) THEN
                    RAISE EXCEPTION 'Clients are registered with secrets held wrapped.';
                END IF;
            END;
            $do$;
            """);

        migrationBuilder.DropColumn(
            name: "secret_issued_at",
            schema: "identity",
            table: "oidc_clients");
    }
}
