using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class KeepARecordForEachGrant : Migration
{
    private static readonly string[] SubjectAndPurpose = ["subject", "purpose"];

    private static readonly string[] SubjectAndRecordedAt = ["subject", "recorded_at"];

    private static readonly string[] SubjectAndGrantedAt = ["subject", "granted_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropPrimaryKey(
            name: "pk_objections",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropPrimaryKey(
            name: "pk_consents",
            schema: "identity",
            table: "consents");

        migrationBuilder.AddColumn<Guid>(
            name: "id",
            schema: "identity",
            table: "objections",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "id",
            schema: "identity",
            table: "consents",
            type: "uuid",
            nullable: true);

        // PRIV-CONS-001, CONV-DESIGN-004: a record written before this was the one row of
        // its subject and purpose, and it stays as it is under an identifier of its own,
        // a version 7 value made from the instant the record carries, as the store makes
        // one for a new record.
        migrationBuilder.Sql(
            """
            UPDATE identity.objections SET id = CAST(
                lpad(to_hex(CAST(floor(extract(epoch FROM recorded_at) * 1000) AS bigint)), 12, '0')
                || '7' || substr(replace(CAST(gen_random_uuid() AS text), '-', ''), 14) AS uuid);
            """);
        migrationBuilder.Sql(
            """
            UPDATE identity.consents SET id = CAST(
                lpad(to_hex(CAST(floor(extract(epoch FROM granted_at) * 1000) AS bigint)), 12, '0')
                || '7' || substr(replace(CAST(gen_random_uuid() AS text), '-', ''), 14) AS uuid);
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "id",
            schema: "identity",
            table: "objections",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "id",
            schema: "identity",
            table: "consents",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AddPrimaryKey(
            name: "pk_objections",
            schema: "identity",
            table: "objections",
            column: "id");

        migrationBuilder.AddPrimaryKey(
            name: "pk_consents",
            schema: "identity",
            table: "consents",
            column: "id");

        migrationBuilder.CreateIndex(
            name: "ix_objections_subject",
            schema: "identity",
            table: "objections",
            columns: SubjectAndRecordedAt);

        migrationBuilder.CreateIndex(
            name: "ux_objections_standing",
            schema: "identity",
            table: "objections",
            columns: SubjectAndPurpose,
            unique: true,
            filter: "withdrawn_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_consents_subject",
            schema: "identity",
            table: "consents",
            columns: SubjectAndGrantedAt);

        migrationBuilder.CreateIndex(
            name: "ux_consents_live",
            schema: "identity",
            table: "consents",
            columns: SubjectAndPurpose,
            unique: true,
            filter: "withdrawn_at IS NULL AND superseded_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropPrimaryKey(
            name: "pk_objections",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropIndex(
            name: "ix_objections_subject",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropIndex(
            name: "ux_objections_standing",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropPrimaryKey(
            name: "pk_consents",
            schema: "identity",
            table: "consents");

        migrationBuilder.DropIndex(
            name: "ix_consents_subject",
            schema: "identity",
            table: "consents");

        migrationBuilder.DropIndex(
            name: "ux_consents_live",
            schema: "identity",
            table: "consents");

        migrationBuilder.DropColumn(
            name: "id",
            schema: "identity",
            table: "objections");

        migrationBuilder.DropColumn(
            name: "id",
            schema: "identity",
            table: "consents");

        // PRIV-CONS-001: the earlier key holds one record a subject and purpose, so it
        // is refused where a subject holds a second; no record is removed to make room.
        migrationBuilder.AddPrimaryKey(
            name: "pk_objections",
            schema: "identity",
            table: "objections",
            columns: SubjectAndPurpose);

        migrationBuilder.AddPrimaryKey(
            name: "pk_consents",
            schema: "identity",
            table: "consents",
            columns: SubjectAndPurpose);
    }
}
