using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AdmitTheRetentionErasureReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // REC-ADM-003, D-223: a host's erasure of a person record it keeps nothing about
        // any longer is an erasure of its own reason, carried by its row and its delivery.
        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_reason",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropCheckConstraint(
            name: "ck_erasures_reason",
            schema: "identity",
            table: "erasures");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_reason",
            schema: "identity",
            table: "outbox",
            sql: "reason IN ('erasure-request', 'minor-takedown', 'retention')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_erasures_reason",
            schema: "identity",
            table: "erasures",
            sql: "reason IN ('erasure-request', 'minor-takedown', 'retention')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_reason",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropCheckConstraint(
            name: "ck_erasures_reason",
            schema: "identity",
            table: "erasures");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_reason",
            schema: "identity",
            table: "outbox",
            sql: "reason IN ('erasure-request', 'minor-takedown')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_erasures_reason",
            schema: "identity",
            table: "erasures",
            sql: "reason IN ('erasure-request', 'minor-takedown')");
    }
}
