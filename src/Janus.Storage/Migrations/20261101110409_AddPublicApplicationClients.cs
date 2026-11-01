using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// AUTH-OIDC-001 (D-252): a public application holds no secret, and alone holds further
/// destinations and an audience; every client registered before holds its secret and one
/// destination, as before.
/// </remarks>
internal sealed partial class AddPublicApplicationClients : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_oidc_clients_kind",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "secret_issued_at",
            schema: "identity",
            table: "oidc_clients",
            type: "timestamp with time zone",
            nullable: true,
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone");

        migrationBuilder.AlterColumn<byte[]>(
            name: "secret",
            schema: "identity",
            table: "oidc_clients",
            type: "bytea",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea");

        migrationBuilder.AddColumn<string>(
            name: "audience",
            schema: "identity",
            table: "oidc_clients",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string[]>(
            name: "other_redirects",
            schema: "identity",
            table: "oidc_clients",
            type: "text[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.AddCheckConstraint(
            name: "ck_oidc_clients_kind",
            schema: "identity",
            table: "oidc_clients",
            sql: "kind IN ('browser-application', 'protocol', 'public-application')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_oidc_clients_public",
            schema: "identity",
            table: "oidc_clients",
            sql: "kind = 'public-application' OR (cardinality(other_redirects) = 0 AND audience IS NULL)");

        migrationBuilder.AddCheckConstraint(
            name: "ck_oidc_clients_secret",
            schema: "identity",
            table: "oidc_clients",
            sql: "((kind = 'public-application') = (secret IS NULL)) AND ((secret IS NULL) = (secret_issued_at IS NULL))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_oidc_clients_kind",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.DropCheckConstraint(
            name: "ck_oidc_clients_public",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.DropCheckConstraint(
            name: "ck_oidc_clients_secret",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.DropColumn(
            name: "audience",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.DropColumn(
            name: "other_redirects",
            schema: "identity",
            table: "oidc_clients");

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "secret_issued_at",
            schema: "identity",
            table: "oidc_clients",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone",
            oldNullable: true);

        migrationBuilder.AlterColumn<byte[]>(
            name: "secret",
            schema: "identity",
            table: "oidc_clients",
            type: "bytea",
            nullable: false,
            defaultValue: Array.Empty<byte>(),
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_oidc_clients_kind",
            schema: "identity",
            table: "oidc_clients",
            sql: "kind IN ('browser-application', 'protocol')");
    }
}
