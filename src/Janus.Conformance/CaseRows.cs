using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;

namespace Janus.Conformance;

/// <summary>
/// The library's own rows a truth-table case needs before it is asked: the
/// organizations, the accounts, the roles, the groups and the grants.
/// </summary>
/// <param name="connect">Opens a connection to the deployment's database.</param>
/// <param name="clock">The deployment's clock, which expiry is read against.</param>
/// <remarks>
/// Implements AUTHZ-TEST-001 and LIB-API-001. The rows are written into the
/// library-owned tables, whose structure is part of the public contract, by
/// hand-written statements rather than through the library's own operations, so what
/// the gate is asked about is a database in a stated shape and not one the gate's own
/// writers produced. The host opens the connection, so the library retrieves none
/// outside its own accessor (OPS-DATA-002).
/// </remarks>
internal sealed class CaseRows(Func<CancellationToken, ValueTask<DbConnection>> connect, TimeProvider clock)
{
    // Every grant a case writes was written before the case is asked, so none of them
    // is judged at the instant it was granted.
    private static readonly TimeSpan Before = TimeSpan.FromHours(2);

    /// <summary>
    /// Writes an organization.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The organization.</returns>
    public async ValueTask<OrganizationId> OrganizationAsync(CancellationToken cancellationToken)
    {
        var organization = OrganizationId.New(clock);

        await ExecuteAsync(
            "INSERT INTO identity.organizations (id, name, created_at) VALUES (@id, @name, @at);",
            new
            {
                id = organization.Value,
                name = "Conformance " + organization.Value.ToString("n", CultureInfo.InvariantCulture),
                at = clock.GetUtcNow() - Before,
            },
            cancellationToken).ConfigureAwait(false);

        return organization;
    }

    /// <summary>
    /// Writes an active account.
    /// </summary>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The subject the account was issued.</returns>
    public async ValueTask<SubjectId> AccountAsync(CancellationToken cancellationToken)
    {
        SubjectId subject;

        using (var randomness = RandomNumberGenerator.Create())
        {
            subject = SubjectId.New(randomness);
        }

        await ExecuteAsync(
            "INSERT INTO identity.accounts (subject, created_at, state) VALUES (@subject, @at, 'active');",
            new { subject = subject.Value, at = clock.GetUtcNow() - Before },
            cancellationToken).ConfigureAwait(false);

        return subject;
    }

    /// <summary>
    /// Writes a role of the case's own.
    /// </summary>
    /// <param name="allows">The one permission it allows, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The role.</returns>
    public async ValueTask<RoleName> RoleAsync(Permission? allows, CancellationToken cancellationToken)
    {
        var role = RoleName.Parse(
            "conformance" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture)[..12]);

        await ExecuteAsync(
            "INSERT INTO identity.roles (name) VALUES (@role);",
            new { role = role.ToString() },
            cancellationToken).ConfigureAwait(false);

        if (allows is Permission permission)
        {
            await AllowAsync(role, permission, cancellationToken).ConfigureAwait(false);
        }

        return role;
    }

    /// <summary>
    /// Has a role allow a permission, writing the role where the deployment holds none
    /// by that name, as the role a derivation confers may not be.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <param name="permission">What it allows from now on.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async ValueTask AllowAsync(RoleName role, Permission permission, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            """
            INSERT INTO identity.roles (name) VALUES (@role) ON CONFLICT DO NOTHING;
            INSERT INTO identity.role_permissions (role, permission)
            VALUES (@role, @permission)
            ON CONFLICT DO NOTHING;
            """,
            new { role = role.ToString(), permission = permission.ToString() },
            cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Writes a group, its one direct member and the closure the membership produces.
    /// </summary>
    /// <param name="organization">The organization the group belongs to.</param>
    /// <param name="member">Its direct member.</param>
    /// <param name="reaches">Every subject it reaches, with the depth it reaches it at.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The group.</returns>
    public async ValueTask<GroupId> GroupAsync(
        OrganizationId organization,
        GrantSubject member,
        IReadOnlyList<(GrantSubject Subject, int Depth)> reaches,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reaches);

        var group = GroupId.New(clock);

        await ExecuteAsync(
            """
            INSERT INTO identity.groups (id, organization, name) VALUES (@id, @organization, @name);
            INSERT INTO identity.group_members (group_id, member_type, member_id)
            VALUES (@id, @memberType, @member);
            """,
            new
            {
                id = group.Value,
                organization = organization.Value,
                name = "Conformance " + group.Value.ToString("n", CultureInfo.InvariantCulture),
                memberType = TypeOf(member),
                member = member.Value,
            },
            cancellationToken).ConfigureAwait(false);

        foreach ((GrantSubject reached, int depth) in reaches)
        {
            await ExecuteAsync(
                """
                INSERT INTO identity.group_closure (group_id, member_type, member_id, depth)
                VALUES (@id, @memberType, @member, @depth);
                """,
                new { id = group.Value, memberType = TypeOf(reached), member = reached.Value, depth },
                cancellationToken).ConfigureAwait(false);
        }

        return group;
    }

    /// <summary>
    /// Writes a stored grant.
    /// </summary>
    /// <param name="grant">What the grant says.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    public async ValueTask GrantAsync(CaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        DateTimeOffset now = clock.GetUtcNow();

        await ExecuteAsync(
            """
            INSERT INTO identity.grants
                (id, subject_type, subject_id, role, organization, resource_type, resource_id,
                 deny, kind, expires_at, granted_by, granted_at, reason,
                 revoked_at, revoked_by, revocation_reason)
            VALUES
                (@id, @subjectType, @subject, @role, @organization, @resourceType, @resourceId,
                 @deny, 'stored', @expiresAt, @grantedBy, @grantedAt, @reason,
                 @revokedAt, @revokedBy, @revocationReason);
            """,
            new
            {
                id = GrantId.New(clock).Value,
                subjectType = TypeOf(grant.Subject),
                subject = grant.Subject.Value,
                role = grant.Role.ToString(),
                organization = grant.Organization.Value,
                resourceType = grant.On?.Type.ToString(),
                resourceId = grant.On?.Id.ToString(),
                deny = grant.Deny,
                expiresAt = grant.Expired ? now - TimeSpan.FromHours(1) : (DateTimeOffset?)null,
                grantedBy = grant.Granter.Value,
                grantedAt = now - Before,
                reason = "conformance",
                revokedAt = grant.Revoked ? now - TimeSpan.FromHours(1) : (DateTimeOffset?)null,
                revokedBy = grant.Revoked ? grant.Granter.Value : (Guid?)null,
                revocationReason = grant.Revoked ? "conformance" : null,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static string TypeOf(GrantSubject subject) =>
        subject.Type == SubjectType.Group ? "group" : "user";

    private async ValueTask ExecuteAsync(
        string statement,
        object arguments,
        CancellationToken cancellationToken)
    {
        DbConnection connection = await connect(cancellationToken).ConfigureAwait(false);

        await using (connection.ConfigureAwait(false))
        {
            _ = await connection.ExecuteAsync(new CommandDefinition(
                statement,
                arguments,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }
}
