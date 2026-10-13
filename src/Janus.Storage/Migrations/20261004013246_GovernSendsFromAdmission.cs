using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class GovernSendsFromAdmission : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // AUTH-ABUSE-004 (D-183): an outbox row is one admitted message in one language,
        // or one mail carrying every declared language, so it keeps no list of the
        // languages taken.
        migrationBuilder.DropColumn(
            name: "taken_languages",
            schema: "identity",
            table: "send_outbox");

        // AUTH-ABUSE-004 AC16, INT-SMS-005: a send that fails for good gives back the
        // credit it spent, so the send keeps which grants it spent and the version of
        // the fingerprint key each stood under. No send counted before this spent any
        // that can be given back.
        migrationBuilder.AddColumn<byte[][]>(
            name: "spent",
            schema: "identity",
            table: "sends",
            type: "bytea[]",
            nullable: false,
            defaultValue: Array.Empty<byte[]>());

        migrationBuilder.AddColumn<int[]>(
            name: "spent_versions",
            schema: "identity",
            table: "sends",
            type: "integer[]",
            nullable: false,
            defaultValue: Array.Empty<int>());

        // CONV-DESIGN-003, INF-BG-001 (D-183): a row is claimed before its handler is
        // called, until this instant; nothing where no attempt holds it.
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "claimed_until",
            schema: "identity",
            table: "send_outbox",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "spent",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "spent_versions",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "claimed_until",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.AddColumn<string>(
            name: "taken_languages",
            schema: "identity",
            table: "send_outbox",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");
    }
}
