using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddSessionCsrfToken : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // BFF-CSRF-001: an existing row gains no usable token, since a zero-length
        // fingerprint matches nothing presented; the next rotation binds one.
        migrationBuilder.AddColumn<byte[]>(
            name: "csrf_fingerprint",
            schema: "identity",
            table: "sessions",
            type: "bytea",
            maxLength: 32,
            nullable: false,
            defaultValue: Array.Empty<byte>());
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "csrf_fingerprint",
            schema: "identity",
            table: "sessions");
    }
}
