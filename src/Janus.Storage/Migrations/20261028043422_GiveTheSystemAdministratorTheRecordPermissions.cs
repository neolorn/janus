using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class GiveTheSystemAdministratorTheRecordPermissions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // `10` section 3 (D-221): the seeded role holds every library-owned permission,
        // so a deployment bootstrap stood up before person records gives it theirs.
        migrationBuilder.Sql(
            """
            INSERT INTO identity.role_permissions (role, permission)
            SELECT roles.name, added.permission
            FROM identity.roles
            CROSS JOIN (VALUES
                ('record:create'),
                ('record:erase'),
                ('record:manage'),
                ('record:read')) AS added (permission)
            WHERE roles.name = 'system-administrator'
            ON CONFLICT (role, permission) DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // The version before knows no person record, so no role keeps a permission over one.
        migrationBuilder.Sql(
            """
            DELETE FROM identity.role_permissions
            WHERE permission IN ('record:create', 'record:erase', 'record:manage', 'record:read');
            """);
    }
}
