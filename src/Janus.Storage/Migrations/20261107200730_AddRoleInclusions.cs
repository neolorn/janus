using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
/// <remarks>
/// AUTHZ-GRANT-006 (D-278): roles' inclusions of one another, kept apart from each role's
/// own permissions and <c>handsOut</c>, and what each role carries and hands out through
/// them, which every decision reads.
/// </remarks>
internal sealed partial class AddRoleInclusions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "role_inclusions",
            schema: "identity",
            columns: table => new
            {
                role = table.Column<string>(type: "text", nullable: false),
                included = table.Column<string>(type: "text", nullable: false),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                left_out_permissions = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                left_out_hands_out = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_role_inclusions", x => new { x.role, x.included });
                table.ForeignKey(
                    name: "fk_role_inclusions_included",
                    column: x => x.included,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "name",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_role_inclusions_role",
                    column: x => x.role,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "name",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "seeded_role_inclusions",
            schema: "identity",
            columns: table => new
            {
                role = table.Column<string>(type: "text", nullable: false),
                included = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_seeded_role_inclusions", x => new { x.role, x.included }));

        migrationBuilder.CreateTable(
            name: "uninherited_permissions",
            schema: "identity",
            columns: table => new
            {
                permission = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_uninherited_permissions", x => x.permission));

        migrationBuilder.CreateIndex(
            name: "ix_role_inclusions_included",
            schema: "identity",
            table: "role_inclusions",
            column: "included");

        // AUTHZ-GRANT-006: a role's effective permissions are its own and, through each
        // inclusion that is on, the included role's, at any depth, save system
        // administration (OPS-CFG-007), an action not inherited and what the inclusion
        // leaves out; with authz.roleinclusion.enabled off, or holding a value that is not
        // "true", its own alone, as OPS-CFG-002 leaves the stricter rule. UNION ends the
        // recursion at the first row it has already reached.
        migrationBuilder.Sql(
            """
            CREATE VIEW identity.role_effective_permissions AS
            WITH RECURSIVE reached (role, permission) AS (
                SELECT p.role, p.permission
                FROM identity.role_permissions AS p
                UNION
                SELECT i.role, r.permission
                FROM identity.role_inclusions AS i
                JOIN reached AS r ON r.role = i.included
                WHERE i.enabled
                  AND r.permission <> 'system:administer'
                  AND NOT (r.permission = ANY (i.left_out_permissions))
                  AND NOT EXISTS (
                      SELECT 1
                      FROM identity.uninherited_permissions AS u
                      WHERE u.permission = r.permission)
                  AND COALESCE(
                      (SELECT s.value FROM identity.settings AS s WHERE s.key = 'authz.roleinclusion.enabled'),
                      'true') = 'true'
            )
            SELECT role, permission FROM reached;
            """);

        // AUTHZ-GRANT-006: a role's effective handsOut, likewise, giving being an ability.
        migrationBuilder.Sql(
            """
            CREATE VIEW identity.role_effective_hands_out AS
            WITH RECURSIVE reached (role, name) AS (
                SELECT r.name, handed.name
                FROM identity.roles AS r
                CROSS JOIN LATERAL unnest(r.hands_out) AS handed (name)
                UNION
                SELECT i.role, h.name
                FROM identity.role_inclusions AS i
                JOIN reached AS h ON h.role = i.included
                WHERE i.enabled
                  AND NOT (h.name = ANY (i.left_out_hands_out))
                  AND COALESCE(
                      (SELECT s.value FROM identity.settings AS s WHERE s.key = 'authz.roleinclusion.enabled'),
                      'true') = 'true'
            )
            SELECT role, name FROM reached;
            """);

        // AUTHZ-GRANT-006: a grant, allow or deny, stored or derived, acts on the role's
        // effective permissions, worked out at each request and never copied into it.
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
            JOIN identity.role_effective_permissions AS p ON p.role = g.role
            JOIN identity.organizations AS o ON o.id = g.organization
            WHERE o.deletion_requested_at IS NULL;
            """);

        // OPS-MIG-003 AC1: the application reads and writes these rows and alters
        // nothing, as it does the tables the earlier migrations named.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON
                identity.role_inclusions,
                identity.seeded_role_inclusions,
                identity.uninherited_permissions
            TO identity_app;
            GRANT SELECT ON identity.role_effective_permissions, identity.role_effective_hands_out TO identity_app;
            """);

        // `10` section 3, OPS-BOOT-002 (D-278): the seeded role holds every library-owned
        // permission, so a deployment bootstrap stood up before inclusions gives it theirs.
        migrationBuilder.Sql(
            """
            INSERT INTO identity.role_permissions (role, permission)
            SELECT roles.name, 'roleinclusion:manage'
            FROM identity.roles
            WHERE roles.name = 'system-administrator'
            ON CONFLICT (role, permission) DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        // The version before knows no inclusion, so no role keeps the permission over one.
        migrationBuilder.Sql(
            """
            DELETE FROM identity.role_permissions
            WHERE permission = 'roleinclusion:manage';
            """);

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

        migrationBuilder.Sql("DROP VIEW identity.role_effective_hands_out;");
        migrationBuilder.Sql("DROP VIEW identity.role_effective_permissions;");

        migrationBuilder.DropTable(
            name: "role_inclusions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "seeded_role_inclusions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "uninherited_permissions",
            schema: "identity");
    }
}
