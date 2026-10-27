using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class DeliverPurposeChanges : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox");

        migrationBuilder.AddColumn<string>(
            name: "change",
            schema: "identity",
            table: "outbox",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "purpose",
            schema: "identity",
            table: "outbox",
            type: "text",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_change",
            schema: "identity",
            table: "outbox",
            sql: "change IN ('granted', 'lifted', 'recorded', 'set', 'superseded', 'withdrawn')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox",
            sql: "kind IN ('consent-changed', 'erasure-requested', 'objection-changed', 'restriction-changed', 'takedown-executed')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_purpose",
            schema: "identity",
            table: "outbox",
            sql: "(kind IN ('consent-changed', 'objection-changed') AND purpose IS NOT NULL AND length(trim(purpose)) > 0 AND change IS NOT NULL) OR (kind NOT IN ('consent-changed', 'objection-changed') AND purpose IS NULL AND change IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_change",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropCheckConstraint(
            name: "ck_outbox_purpose",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropColumn(
            name: "change",
            schema: "identity",
            table: "outbox");

        migrationBuilder.DropColumn(
            name: "purpose",
            schema: "identity",
            table: "outbox");

        migrationBuilder.AddCheckConstraint(
            name: "ck_outbox_kind",
            schema: "identity",
            table: "outbox",
            sql: "kind IN ('erasure-requested', 'restriction-changed', 'takedown-executed')");
    }
}
