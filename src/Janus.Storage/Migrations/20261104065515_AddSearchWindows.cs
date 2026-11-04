using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// IDN-SRCH-002 (D-256): each account's staff searches a minute, one row a minute.
/// </remarks>
internal sealed partial class AddSearchWindows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "search_windows",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                count = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_search_windows", x => new { x.subject, x.window_start });
                table.CheckConstraint("ck_search_windows_subject_not_max_uuid", "subject <> 'ffffffff-ffff-ffff-ffff-ffffffffffff'");
            });

        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON identity.search_windows TO identity_app;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "search_windows",
            schema: "identity");
    }
}
