using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class HaltAccessThroughSuspendedOrganizations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // IDN-ORG-003 AC1: every decision reads this view, so a grant of an
        // organization whose deletion was requested stops conferring on the next
        // request, and one whose request is cancelled confers again. An erased
        // organization keeps its request instant, so it stays out.
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE VIEW identity.effective_grants AS
            SELECT g.id            AS grant_id,
                   g.subject_type  AS subject_type,
                   g.subject_id    AS subject_id,
                   g.role          AS role,
                   p.permission    AS permission,
                   g.resource_type AS resource_type,
                   g.resource_id   AS resource_id,
                   g.deny          AS deny,
                   g.kind          AS kind,
                   g.organization  AS organization,
                   g.expires_at    AS expires_at,
                   g.revoked_at    AS revoked_at
            FROM identity.grants AS g
            JOIN identity.role_permissions AS p ON p.role = g.role
            JOIN identity.organizations AS o ON o.id = g.organization
            WHERE o.deletion_requested_at IS NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql(
            """
            CREATE OR REPLACE VIEW identity.effective_grants AS
            SELECT g.id            AS grant_id,
                   g.subject_type  AS subject_type,
                   g.subject_id    AS subject_id,
                   g.role          AS role,
                   p.permission    AS permission,
                   g.resource_type AS resource_type,
                   g.resource_id   AS resource_id,
                   g.deny          AS deny,
                   g.kind          AS kind,
                   g.organization  AS organization,
                   g.expires_at    AS expires_at,
                   g.revoked_at    AS revoked_at
            FROM identity.grants AS g
            JOIN identity.role_permissions AS p ON p.role = g.role;
            """);
    }
}
