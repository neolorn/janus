using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RecordMembershipAcknowledgements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "acknowledged_at",
            schema: "identity",
            table: "memberships",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "acknowledged_documents",
            schema: "identity",
            table: "memberships",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_memberships_acknowledged",
            schema: "identity",
            table: "memberships",
            sql: "(acknowledged_at IS NULL) = (acknowledged_documents IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_memberships_acknowledged",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "acknowledged_at",
            schema: "identity",
            table: "memberships");

        migrationBuilder.DropColumn(
            name: "acknowledged_documents",
            schema: "identity",
            table: "memberships");
    }
}
