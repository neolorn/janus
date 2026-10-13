using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class RecordTheScopeOfARaisedAlert : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // OPS-ALERT-002, D-177: an alert raised before this carried its scope, where it had
        // one, only inside its key, so its row reads back with none.
        migrationBuilder.AddColumn<string>(
            name: "scope",
            schema: "identity",
            table: "raised_alerts",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropColumn(
            name: "scope",
            schema: "identity",
            table: "raised_alerts");
    }
}
