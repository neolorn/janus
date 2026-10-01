using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RetireTheOrganizationErasureReason : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // IDN-ORG-003, D-166 (155): an organization's erasure erases no account, so no
        // erasure carries it as a reason; the constraint fails where one ever did.
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
            sql: "reason IN ('erasure-request', 'minor-takedown', 'organization-erasure')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_erasures_reason",
            schema: "identity",
            table: "erasures",
            sql: "reason IN ('erasure-request', 'minor-takedown', 'organization-erasure')");
    }
}
