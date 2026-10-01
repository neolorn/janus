using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HoldOneCurrentMembershipOfAnOrganization : Migration
{
    private static readonly string[] SubjectAndOrganization = ["subject", "organization"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateIndex(
            name: "ux_memberships_current",
            schema: "identity",
            table: "memberships",
            columns: SubjectAndOrganization,
            unique: true,
            filter: "ended_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropIndex(
            name: "ux_memberships_current",
            schema: "identity",
            table: "memberships");
    }
}
