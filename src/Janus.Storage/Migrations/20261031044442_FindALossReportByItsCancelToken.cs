using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class FindALossReportByItsCancelToken : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<byte[]>(
            name: "cancel_fingerprint",
            schema: "identity",
            table: "loss_reports",
            type: "bytea",
            nullable: true);

        // D-226, OPS-MIG-005 criterion 3: a report open before this holds a random
        // value no token hashes to, and is cancelled from a session.
        migrationBuilder.Sql(
            """
            UPDATE identity.loss_reports SET cancel_fingerprint = sha256(
                convert_to(CAST(gen_random_uuid() AS text) || CAST(gen_random_uuid() AS text), 'UTF8'));
            """);

        migrationBuilder.AlterColumn<byte[]>(
            name: "cancel_fingerprint",
            schema: "identity",
            table: "loss_reports",
            type: "bytea",
            nullable: false,
            oldClrType: typeof(byte[]),
            oldType: "bytea",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_loss_reports_cancel_fingerprint",
            schema: "identity",
            table: "loss_reports",
            column: "cancel_fingerprint",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropIndex(
            name: "ux_loss_reports_cancel_fingerprint",
            schema: "identity",
            table: "loss_reports");

        migrationBuilder.DropColumn(
            name: "cancel_fingerprint",
            schema: "identity",
            table: "loss_reports");
    }
}
