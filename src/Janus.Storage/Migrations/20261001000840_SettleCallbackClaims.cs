using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class SettleCallbackClaims : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // BFF-MACH-002 AC3, entry 276: a claim is settled once its event is carried. A
        // claim held before this was kept only where its route carried the event or
        // nothing told it otherwise, so each is read as settled when it was claimed, and
        // a later delivery of its event is acknowledged as it was before.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "settled_at",
            schema: "identity",
            table: "callback_events",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("UPDATE identity.callback_events SET settled_at = claimed_at;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "settled_at",
            schema: "identity",
            table: "callback_events");
    }
}
