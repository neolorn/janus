using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HoldSigningKeysThroughTheirKeeping : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-KEY-001, D-181: a key is next, current or replaced; a replaced key keeps
        // its public key until its keeping ends and its private key until its overlap
        // ends. Every key held before this signed from when it was made. Its longest
        // lifetime is not known, so it takes the ceiling of oidc.accesstoken.lifetime,
        // the one value no access token it signed can have outlived, and a replaced key
        // is kept for the ceiling of session.default.absolute from its replacement.
        migrationBuilder.DropCheckConstraint(
            name: "ck_signing_keys_retirement",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.AlterColumn<byte[]>(
            name: "private_key",
            schema: "identity",
            table: "signing_keys",
            type: "bytea",
            nullable: true,
            oldClrType: typeof(byte[]),
            oldType: "bytea");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "kept_until",
            schema: "identity",
            table: "signing_keys",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<TimeSpan>(
            name: "longest_lifetime",
            schema: "identity",
            table: "signing_keys",
            type: "interval",
            nullable: false,
            defaultValue: new TimeSpan(0, 0, 0, 0, 0));

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "signing_from",
            schema: "identity",
            table: "signing_keys",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE identity.signing_keys
            SET signing_from = created_at,
                longest_lifetime = interval '1 hour',
                kept_until = CASE
                    WHEN superseded_at IS NULL THEN NULL
                    ELSE GREATEST(superseded_at + interval '365 days', retires_at)
                END;
            """);

        migrationBuilder.AddColumn<bool>(
            name: "is_current",
            schema: "identity",
            table: "signing_keys",
            type: "boolean",
            nullable: false,
            computedColumnSql: "signing_from IS NOT NULL AND superseded_at IS NULL",
            stored: true);

        migrationBuilder.AddColumn<bool>(
            name: "is_next",
            schema: "identity",
            table: "signing_keys",
            type: "boolean",
            nullable: false,
            computedColumnSql: "signing_from IS NULL",
            stored: true);

        migrationBuilder.CreateIndex(
            name: "ux_signing_keys_current",
            schema: "identity",
            table: "signing_keys",
            column: "is_current",
            unique: true,
            filter: "is_current");

        migrationBuilder.CreateIndex(
            name: "ux_signing_keys_next",
            schema: "identity",
            table: "signing_keys",
            column: "is_next",
            unique: true,
            filter: "is_next");

        migrationBuilder.AddCheckConstraint(
            name: "ck_signing_keys_private_key",
            schema: "identity",
            table: "signing_keys",
            sql: "private_key IS NOT NULL OR retires_at IS NOT NULL");

        migrationBuilder.AddCheckConstraint(
            name: "ck_signing_keys_stage",
            schema: "identity",
            table: "signing_keys",
            sql: "(signing_from IS NULL AND longest_lifetime = interval '0' AND superseded_at IS NULL AND retires_at IS NULL AND kept_until IS NULL) OR (signing_from >= created_at AND superseded_at IS NULL AND retires_at IS NULL AND kept_until IS NULL) OR (signing_from >= created_at AND superseded_at >= signing_from AND retires_at > superseded_at AND kept_until >= retires_at)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // The earlier schema holds neither a key that signs nothing yet nor a key whose
        // private key is gone; a next key has signed nothing and a retired key signs
        // nothing more, so both go.
        migrationBuilder.Sql(
            """
            DELETE FROM identity.signing_keys
            WHERE signing_from IS NULL OR private_key IS NULL;
            """);

        migrationBuilder.DropIndex(
            name: "ux_signing_keys_current",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropIndex(
            name: "ux_signing_keys_next",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropCheckConstraint(
            name: "ck_signing_keys_private_key",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropCheckConstraint(
            name: "ck_signing_keys_stage",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropColumn(
            name: "is_current",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropColumn(
            name: "is_next",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropColumn(
            name: "kept_until",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropColumn(
            name: "longest_lifetime",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.DropColumn(
            name: "signing_from",
            schema: "identity",
            table: "signing_keys");

        migrationBuilder.AlterColumn<byte[]>(
            name: "private_key",
            schema: "identity",
            table: "signing_keys",
            type: "bytea",
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_signing_keys_retirement",
            schema: "identity",
            table: "signing_keys",
            sql: "(superseded_at IS NULL AND retires_at IS NULL) OR (superseded_at IS NOT NULL AND retires_at > superseded_at)");
    }
}
