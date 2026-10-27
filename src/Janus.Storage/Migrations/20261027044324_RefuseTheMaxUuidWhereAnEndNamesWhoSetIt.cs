using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RefuseTheMaxUuidWhereAnEndNamesWhoSetIt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.AddCheckConstraint(
            name: "ck_memberships_end_set_by_not_max_uuid",
            schema: "identity",
            table: "memberships",
            sql: "end_set_by <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropCheckConstraint(
            name: "ck_memberships_end_set_by_not_max_uuid",
            schema: "identity",
            table: "memberships");
    }
}
