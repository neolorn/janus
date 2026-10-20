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
