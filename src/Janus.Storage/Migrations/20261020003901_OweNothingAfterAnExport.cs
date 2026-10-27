using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class OweNothingAfterAnExport : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // PRIV-RIGHT-003 (D-203): the export carries the host's part when it is
        // assembled, so nothing is owed after it; a delivery an earlier version wrote
        // for an export, and its confirmations, would refuse the narrowed kinds.
        migrationBuilder.Sql(
            """
            DELETE FROM identity.outbox_confirmations
            WHERE delivery IN (SELECT id FROM identity.outbox WHERE kind = 'export-requested');
            DELETE FROM identity.outbox WHERE kind = 'export-requested';
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox",
            sql: "kind IN ('erasure-requested', 'restriction-changed', 'takedown-executed')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox",
            sql: "kind IN ('erasure-requested', 'export-requested', 'restriction-changed', 'takedown-executed')");
    }
}
