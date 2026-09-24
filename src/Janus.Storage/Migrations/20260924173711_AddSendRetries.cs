using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSendRetries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_send_outbox_recorded_at",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.AddColumn<int>(
            name: "attempts",
            schema: "identity",
            table: "send_outbox",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "next_attempt_at",
            schema: "identity",
            table: "send_outbox",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

        migrationBuilder.AddColumn<string>(
            name: "taken_languages",
            schema: "identity",
            table: "send_outbox",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        // A message recorded before the publisher existed is due at once.
        migrationBuilder.Sql("UPDATE identity.send_outbox SET next_attempt_at = recorded_at;");

        migrationBuilder.CreateIndex(
            name: "ix_send_outbox_due",
            schema: "identity",
            table: "send_outbox",
            column: "next_attempt_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_send_outbox_due",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.DropColumn(
            name: "attempts",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.DropColumn(
            name: "next_attempt_at",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.DropColumn(
            name: "taken_languages",
            schema: "identity",
            table: "send_outbox");

        migrationBuilder.CreateIndex(
            name: "ix_send_outbox_recorded_at",
            schema: "identity",
            table: "send_outbox",
            column: "recorded_at");
    }
}
