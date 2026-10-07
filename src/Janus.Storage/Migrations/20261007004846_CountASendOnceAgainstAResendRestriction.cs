using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class CountASendOnceAgainstAResendRestriction : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<bool>(
            name: "admitted",
            schema: "identity",
            table: "sends",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<byte[]>(
            name: "answer",
            schema: "identity",
            table: "sends",
            type: "bytea",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<byte[][]>(
            name: "resend",
            schema: "identity",
            table: "sends",
            type: "bytea[]",
            nullable: false,
            defaultValue: Array.Empty<byte[]>());

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "resend_at",
            schema: "identity",
            table: "sends",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "second_factor",
            schema: "identity",
            table: "sends",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<byte[]>(
            name: "send",
            schema: "identity",
            table: "sends",
            type: "bytea",
            maxLength: 32,
            nullable: false,
            defaultValue: Array.Empty<byte>());

        // AUTH-ABUSE-004 (D-197): a message counted before this runs is the one message
        // of its send and holds the count of its admission; no restriction marked resend
        // stood when it was counted, so it holds no count of that kind.
        migrationBuilder.Sql(
            """
            UPDATE identity.sends SET send = reference, admitted = TRUE;
            """);

        migrationBuilder.CreateIndex(
            name: "ix_sends_answer",
            schema: "identity",
            table: "sends",
            column: "answer");

        migrationBuilder.CreateIndex(
            name: "ix_sends_send",
            schema: "identity",
            table: "sends",
            column: "send");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropIndex(
            name: "ix_sends_answer",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropIndex(
            name: "ix_sends_send",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "admitted",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "answer",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "resend",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "resend_at",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "second_factor",
            schema: "identity",
            table: "sends");

        migrationBuilder.DropColumn(
            name: "send",
            schema: "identity",
            table: "sends");
    }
}
