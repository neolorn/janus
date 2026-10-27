using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class GiveTheSystemAdministratorTheReads : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // `10` section 3 (D-218): the seeded role holds every library-owned permission,
        // so a deployment bootstrap stood up before the reads existed gives it them.
        migrationBuilder.Sql(
            """
            INSERT INTO identity.role_permissions (role, permission)
            SELECT roles.name, added.permission
            FROM identity.roles
            CROSS JOIN (VALUES
                ('account:read'),
                ('consent:read'),
                ('contact:read'),
                ('membership:read'),
                ('organization:read')) AS added (permission)
            WHERE roles.name = 'system-administrator'
            ON CONFLICT (role, permission) DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // The version before knows none of the reads, so no role keeps one.
        migrationBuilder.Sql(
            """
            DELETE FROM identity.role_permissions
            WHERE permission IN
                ('account:read', 'consent:read', 'contact:read', 'membership:read', 'organization:read');
            """);
    }
}
