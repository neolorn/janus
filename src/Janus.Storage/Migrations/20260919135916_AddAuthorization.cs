using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Janus.Storage.Migrations;

/// <inheritdoc />
internal sealed partial class AddAuthorization : Migration
{
    private static readonly string[] AncestorTypeAndId = ["ancestor_type", "ancestor_id"];
    private static readonly string[] ContainedInTypeAndId = ["contained_in_type", "contained_in_id"];
    private static readonly string[] MemberTypeAndId = ["member_type", "member_id"];
    private static readonly string[] OrganizationAndSubject = ["organization", "subject_type", "subject_id"];
    private static readonly string[] ResourceTypeAndId = ["resource_type", "resource_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "ancestry",
            schema: "identity",
            columns: table => new
            {
                resource_type = table.Column<string>(type: "text", nullable: false),
                resource_id = table.Column<string>(type: "text", nullable: false),
                ancestor_type = table.Column<string>(type: "text", nullable: false),
                ancestor_id = table.Column<string>(type: "text", nullable: false),
                depth = table.Column<int>(type: "integer", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ancestry", x => new { x.resource_type, x.resource_id, x.ancestor_type, x.ancestor_id });
                table.CheckConstraint("ck_ancestry_depth", "depth >= 0");
            });

        migrationBuilder.CreateTable(
            name: "grant_versions",
            schema: "identity",
            columns: table => new
            {
                subject = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_grant_versions", x => x.subject);
                table.CheckConstraint("ck_grant_versions_version", "version >= 0");
                table.ForeignKey(
                    name: "fk_grant_versions_subject",
                    column: x => x.subject,
                    principalSchema: "identity",
                    principalTable: "accounts",
                    principalColumn: "subject",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "groups",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_groups", x => x.id);
                table.ForeignKey(
                    name: "fk_groups_organization",
                    column: x => x.organization,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "resources",
            schema: "identity",
            columns: table => new
            {
                resource_type = table.Column<string>(type: "text", nullable: false),
                resource_id = table.Column<string>(type: "text", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false),
                contained_in_type = table.Column<string>(type: "text", nullable: true),
                contained_in_id = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_resources", x => new { x.resource_type, x.resource_id });
                table.CheckConstraint("ck_resources_contained_in", "(contained_in_type IS NULL) = (contained_in_id IS NULL)");
                table.ForeignKey(
                    name: "fk_resources_organization",
                    column: x => x.organization,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "roles",
            schema: "identity",
            columns: table => new
            {
                name = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_roles", x => x.name));

        migrationBuilder.CreateTable(
            name: "group_closure",
            schema: "identity",
            columns: table => new
            {
                group_id = table.Column<Guid>(type: "uuid", nullable: false),
                member_type = table.Column<string>(type: "text", nullable: false),
                member_id = table.Column<Guid>(type: "uuid", nullable: false),
                depth = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_group_closure", x => new { x.group_id, x.member_type, x.member_id });
                table.CheckConstraint("ck_group_closure_depth", "depth >= 1");
                table.CheckConstraint("ck_group_closure_member_type", "member_type IN ('group', 'user')");
                table.ForeignKey(
                    name: "fk_group_closure_group",
                    column: x => x.group_id,
                    principalSchema: "identity",
                    principalTable: "groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "group_members",
            schema: "identity",
            columns: table => new
            {
                group_id = table.Column<Guid>(type: "uuid", nullable: false),
                member_type = table.Column<string>(type: "text", nullable: false),
                member_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_group_members", x => new { x.group_id, x.member_type, x.member_id });
                table.CheckConstraint("ck_group_members_member_type", "member_type IN ('group', 'user')");
                table.ForeignKey(
                    name: "fk_group_members_group",
                    column: x => x.group_id,
                    principalSchema: "identity",
                    principalTable: "groups",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "grants",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                subject_type = table.Column<string>(type: "text", nullable: false),
                subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                role = table.Column<string>(type: "text", nullable: false),
                organization = table.Column<Guid>(type: "uuid", nullable: false),
                resource_type = table.Column<string>(type: "text", nullable: true),
                resource_id = table.Column<string>(type: "text", nullable: true),
                deny = table.Column<bool>(type: "boolean", nullable: false),
                kind = table.Column<string>(type: "text", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "text", nullable: false),
                revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                revocation_reason = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_grants", x => x.id);
                table.CheckConstraint("ck_grants_kind", "kind IN ('derived', 'materialised', 'stored')");
                table.CheckConstraint("ck_grants_reason", "length(btrim(reason)) BETWEEN 1 AND 1024");
                table.CheckConstraint("ck_grants_resource", "(resource_type IS NULL) = (resource_id IS NULL)");
                table.CheckConstraint("ck_grants_revocation", "(revoked_at IS NULL AND revoked_by IS NULL AND revocation_reason IS NULL)\nOR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL\n    AND length(btrim(revocation_reason)) BETWEEN 1 AND 1024)");
                table.CheckConstraint("ck_grants_subject_type", "subject_type IN ('group', 'user')");
                table.ForeignKey(
                    name: "fk_grants_organization",
                    column: x => x.organization,
                    principalSchema: "identity",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_grants_role",
                    column: x => x.role,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "name",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "role_permissions",
            schema: "identity",
            columns: table => new
            {
                role = table.Column<string>(type: "text", nullable: false),
                permission = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_role_permissions", x => new { x.role, x.permission });
                table.ForeignKey(
                    name: "fk_role_permissions_role",
                    column: x => x.role,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "name",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_ancestry_ancestor",
            schema: "identity",
            table: "ancestry",
            columns: AncestorTypeAndId);

        migrationBuilder.CreateIndex(
            name: "ix_grants_live_holder",
            schema: "identity",
            table: "grants",
            columns: OrganizationAndSubject,
            filter: "revoked_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_grants_live_resource",
            schema: "identity",
            table: "grants",
            columns: ResourceTypeAndId,
            filter: "revoked_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_grants_role",
            schema: "identity",
            table: "grants",
            column: "role");

        migrationBuilder.CreateIndex(
            name: "ix_group_closure_member",
            schema: "identity",
            table: "group_closure",
            columns: MemberTypeAndId);

        migrationBuilder.CreateIndex(
            name: "ix_group_members_member",
            schema: "identity",
            table: "group_members",
            columns: MemberTypeAndId);

        migrationBuilder.CreateIndex(
            name: "ix_groups_organization",
            schema: "identity",
            table: "groups",
            column: "organization");

        migrationBuilder.CreateIndex(
            name: "ix_resources_contained_in",
            schema: "identity",
            table: "resources",
            columns: ContainedInTypeAndId);

        migrationBuilder.CreateIndex(
            name: "ix_resources_organization",
            schema: "identity",
            table: "resources",
            column: "organization");

        // AUTHZ-GATE-002, AUTHZ-CACHE-001, LIB-API-001: what the grants in force allow,
        // one row per grant and permission. It is a view rather than a table so that
        // editing a role takes effect at once and nothing is stored twice.
        migrationBuilder.Sql(
            """
            CREATE VIEW identity.effective_grants AS
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

        // OPS-MIG-003 AC1: the application reads and writes these rows and alters
        // nothing, as it does the tables the earlier migration named.
        migrationBuilder.Sql(
            """
            GRANT SELECT, INSERT, UPDATE, DELETE ON
                identity.ancestry,
                identity.grants,
                identity.grant_versions,
                identity.group_closure,
                identity.group_members,
                identity.groups,
                identity.resources,
                identity.role_permissions,
                identity.roles
            TO identity_app;
            """);

        migrationBuilder.Sql("GRANT SELECT ON identity.effective_grants TO identity_app;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.Sql("DROP VIEW identity.effective_grants;");

        migrationBuilder.DropTable(
            name: "ancestry",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "grant_versions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "grants",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "group_closure",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "group_members",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "resources",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "role_permissions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "groups",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "roles",
            schema: "identity");
    }
}
