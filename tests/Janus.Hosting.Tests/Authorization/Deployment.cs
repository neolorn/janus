using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Npgsql;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The rows a truth-table case needs written before it can be evaluated: the
/// organization, the accounts, the groups, the host's records and the grants.
/// </summary>
/// <param name="fixture">The deployment the rows are written to.</param>
/// <remarks>
/// The rows are written through hand-written statements rather than through the
/// library's own ports, so that what the gate reads is a database in a stated shape and
/// not a database the gate's own writers produced.
/// </remarks>
internal sealed class Deployment(HostFixture fixture)
{
    /// <summary>
    /// The instant every case is evaluated at.
    /// </summary>
    public static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private SubjectId _granter;

    /// <summary>
    /// The organization every row of the case belongs to.
    /// </summary>
    public OrganizationId Organization { get; } = new(Guid.NewGuid());

    /// <summary>
    /// Writes the organization and the role the grants name.
    /// </summary>
    /// <param name="permissions">What the role allows.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The role.</returns>
    public async Task<RoleName> BeginAsync(
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        var role = RoleName.Parse("role" + Guid.NewGuid().ToString("n")[..8]);

        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO janus.organizations (id, name, created_at)
            VALUES (@organization, @name, @at);
            INSERT INTO janus.roles (name) VALUES (@role);
            """,
            new
            {
                organization = Organization.Value,
                name = "Organization " + Organization.Value.ToString("n", CultureInfo.InvariantCulture),
                at = Noon,
                role = role.ToString(),
            },
            cancellationToken: cancellationToken));

        _granter = await AccountAsync(cancellationToken);

        foreach (Permission permission in permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO janus.role_permissions (role, permission) VALUES (@role, @permission);",
                new { role = role.ToString(), permission = permission.ToString() },
                cancellationToken: cancellationToken));
        }

        return role;
    }

    /// <summary>
    /// Writes a further role of the organization.
    /// </summary>
    /// <param name="permissions">What the role allows.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The role.</returns>
    public async Task<RoleName> RoleAsync(
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var role = RoleName.Parse("role" + Guid.NewGuid().ToString("n")[..8]);

        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO janus.roles (name) VALUES (@role);",
            new { role = role.ToString() },
            cancellationToken: cancellationToken));

        foreach (Permission permission in permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO janus.role_permissions (role, permission) VALUES (@role, @permission);",
                new { role = role.ToString(), permission = permission.ToString() },
                cancellationToken: cancellationToken));
        }

        return role;
    }

    /// <summary>
    /// Writes the role a derivation confers, which the declaration names at startup
    /// and which the deployment fills with what it allows.
    /// </summary>
    /// <param name="name">The role the derivation names.</param>
    /// <param name="permissions">What the role allows.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The role.</returns>
    public async Task<RoleName> NamedRoleAsync(
        RoleName name,
        IReadOnlyList<Permission> permissions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO janus.roles (name) VALUES (@role) ON CONFLICT DO NOTHING;",
            new { role = name.ToString() },
            cancellationToken: cancellationToken));

        foreach (Permission permission in permissions)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO janus.role_permissions (role, permission)
                VALUES (@role, @permission)
                ON CONFLICT DO NOTHING;
                """,
                new { role = name.ToString(), permission = permission.ToString() },
                cancellationToken: cancellationToken));
        }

        return name;
    }

    /// <summary>
    /// Writes the host's own fact that a subject reviews what is in a workspace.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="reviewer">The person reviewing what is in it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task ReviewAsync(
        ResourceReference workspace,
        SubjectId reviewer,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO host.reviewers (workspace_id, reviewer)
            VALUES (@workspace, @reviewer);
            """,
            new { workspace = workspace.Id.ToString(), reviewer = reviewer.Value },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Takes the host's own fact away again.
    /// </summary>
    /// <param name="workspace">The workspace.</param>
    /// <param name="reviewer">The person who was reviewing what is in it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of removing it.</returns>
    public async Task UnreviewAsync(
        ResourceReference workspace,
        SubjectId reviewer,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM host.reviewers
            WHERE workspace_id = @workspace AND reviewer = @reviewer;
            """,
            new { workspace = workspace.Id.ToString(), reviewer = reviewer.Value },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Writes an account.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The subject the account was issued.</returns>
    public async Task<SubjectId> AccountAsync(CancellationToken cancellationToken)
    {
        SubjectId subject;

        using (var randomness = RandomNumberGenerator.Create())
        {
            subject = SubjectId.New(randomness);
        }

        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO janus.accounts (subject, created_at, state)
            VALUES (@subject, @at, 'active');
            """,
            new { subject = subject.Value, at = Noon },
            cancellationToken: cancellationToken));

        return subject;
    }

    /// <summary>
    /// Restricts an account's processing, as a data subject's request does.
    /// </summary>
    /// <param name="subject">The account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task RestrictAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE janus.accounts SET state = 'restricted' WHERE subject = @subject;",
            new { subject = subject.Value },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Writes a group.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The group.</returns>
    public async Task<GroupId> GroupAsync(CancellationToken cancellationToken)
    {
        var group = GroupId.New(TimeProvider.System);

        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO janus.groups (id, organization, name) VALUES (@id, @organization, @name);",
            new
            {
                id = group.Value,
                organization = Organization.Value,
                name = "Group " + group.Value.ToString("n", CultureInfo.InvariantCulture),
            },
            cancellationToken: cancellationToken));

        return group;
    }

    /// <summary>
    /// Puts a subject in a group, writing the closure the change produces.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="member">The account or the group joining it.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task JoinAsync(
        GroupId group,
        GrantSubject member,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO janus.group_members (group_id, member_type, member_id)
            VALUES (@group, @type, @member);

            DELETE FROM janus.group_closure
            WHERE group_id IN (
                SELECT id FROM janus.groups WHERE organization = @organization);

            WITH RECURSIVE reach AS (
                SELECT edge.group_id AS root, edge.member_type, edge.member_id, 1 AS depth
                FROM janus.group_members AS edge
                JOIN janus.groups AS held
                  ON held.id = edge.group_id AND held.organization = @organization
                UNION ALL
                SELECT reach.root, edge.member_type, edge.member_id, reach.depth + 1
                FROM reach
                JOIN janus.group_members AS edge ON edge.group_id = reach.member_id
                WHERE reach.member_type = 'group' AND reach.depth < 64
            )
            INSERT INTO janus.group_closure (group_id, member_type, member_id, depth)
            SELECT root, member_type, member_id, MIN(depth)
            FROM reach
            GROUP BY root, member_type, member_id;
            """,
            new
            {
                group = group.Value,
                type = member.Type == SubjectType.Group ? "group" : "user",
                member = member.Value,
                organization = Organization.Value,
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Registers one of the host's records with the library and writes its ancestry.
    /// </summary>
    /// <param name="reference">The record.</param>
    /// <param name="containedIn">What contains it, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task RegisterAsync(
        ResourceReference reference,
        ResourceReference? containedIn,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO janus.resources
                (resource_type, resource_id, organization, contained_in_type, contained_in_id)
            VALUES (@type, @id, @organization, @containerType, @containerId);
            INSERT INTO janus.ancestry
                (resource_type, resource_id, ancestor_type, ancestor_id, depth, organization)
            SELECT @type, @id, @type, @id, 0, @organization
            UNION ALL
            SELECT @type, @id, above.ancestor_type, above.ancestor_id, above.depth + 1,
                   @organization
            FROM janus.ancestry AS above
            WHERE above.resource_type = @containerType AND above.resource_id = @containerId;
            """,
            new
            {
                type = reference.Type.ToString(),
                id = reference.Id.ToString(),
                organization = Organization.Value,
                containerType = containedIn?.Type.ToString(),
                containerId = containedIn?.Id.ToString(),
            },
            cancellationToken: cancellationToken));

        if (reference.Type == ResourceType.Parse("document"))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO host.documents (id, title) VALUES (@id, @title);",
                new { id = reference.Id.ToString(), title = "A record of the host's." },
                cancellationToken: cancellationToken));
        }
    }

    /// <summary>
    /// Moves a record under another container, rewriting its ancestry.
    /// </summary>
    /// <param name="reference">The record that moves. It holds nothing beneath it.</param>
    /// <param name="containedIn">Its new container.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task MoveAsync(
        ResourceReference reference,
        ResourceReference containedIn,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE janus.resources
            SET contained_in_type = @containerType, contained_in_id = @containerId
            WHERE resource_type = @type AND resource_id = @id;

            DELETE FROM janus.ancestry
            WHERE resource_type = @type AND resource_id = @id AND depth > 0;

            INSERT INTO janus.ancestry
                (resource_type, resource_id, ancestor_type, ancestor_id, depth, organization)
            SELECT @type, @id, above.ancestor_type, above.ancestor_id, above.depth + 1,
                   @organization
            FROM janus.ancestry AS above
            WHERE above.resource_type = @containerType AND above.resource_id = @containerId;
            """,
            new
            {
                type = reference.Type.ToString(),
                id = reference.Id.ToString(),
                containerType = containedIn.Type.ToString(),
                containerId = containedIn.Id.ToString(),
                organization = Organization.Value,
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Revokes a grant.
    /// </summary>
    /// <param name="grant">Which grant.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task RevokeAsync(GrantId grant, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE janus.grants
            SET revoked_at = @at, revoked_by = @by, revocation_reason = @reason
            WHERE id = @id;
            """,
            new
            {
                id = grant.Value,
                at = Noon,
                by = _granter.Value,
                reason = "The reason the grant was taken away.",
            },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Adds a permission to a role, or takes one away.
    /// </summary>
    /// <param name="role">Which role.</param>
    /// <param name="permission">Which permission.</param>
    /// <param name="allows">Whether the role allows it afterwards.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async Task AllowAsync(
        RoleName role,
        Permission permission,
        bool allows,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            allows
                ? "INSERT INTO janus.role_permissions (role, permission) VALUES (@role, @permission);"
                : "DELETE FROM janus.role_permissions WHERE role = @role AND permission = @permission;",
            new { role = role.ToString(), permission = permission.ToString() },
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Writes a grant.
    /// </summary>
    /// <param name="subject">Who holds it.</param>
    /// <param name="role">The role it confers.</param>
    /// <param name="on">The record it is on, or nothing for the whole organization.</param>
    /// <param name="deny">Whether it takes access away.</param>
    /// <param name="expiresAt">When it stops conferring anything.</param>
    /// <param name="organization">The organization it is scoped to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The grant.</returns>
    public async Task<GrantId> GrantAsync(
        GrantSubject subject,
        RoleName role,
        ResourceReference? on,
        bool deny,
        DateTimeOffset? expiresAt,
        OrganizationId? organization,
        CancellationToken cancellationToken)
    {
        var id = GrantId.New(TimeProvider.System);

        await using NpgsqlConnection connection = await fixture.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO janus.grants
                (id, subject_type, subject_id, role, organization, resource_type,
                 resource_id, deny, kind, expires_at, granted_by, granted_at, reason)
            VALUES (@id, @subjectType, @subject, @role, @organization, @resourceType,
                    @resourceId, @deny, 'stored', @expiresAt, @grantedBy, @at, @reason);
            """,
            new
            {
                id = id.Value,
                subjectType = subject.Type == SubjectType.Group ? "group" : "user",
                subject = subject.Value,
                role = role.ToString(),
                organization = (organization ?? Organization).Value,
                resourceType = on?.Type.ToString(),
                resourceId = on?.Id.ToString(),
                deny,
                expiresAt,
                grantedBy = _granter.Value,
                at = Noon,
                reason = "The reason the grant was written.",
            },
            cancellationToken: cancellationToken));

        return id;
    }
}
