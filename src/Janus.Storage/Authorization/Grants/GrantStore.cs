using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Grants;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// Grants, over the <c>grants</c> table, with the counters a change orphans a cache
/// entry by.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="connections">Where the counter statement takes its connection from.</param>
/// <remarks>
/// Implements AUTHZ-GRANT-001, AUTHZ-GRANT-003, AUTHZ-CACHE-001 and CONV-DESIGN-003.
/// A read returns the rows a principal holds and never a resolved outcome; expiry and
/// revocation are carried on the row and read where the question is asked.
/// </remarks>
internal sealed class GrantStore(StoreContext context, DataConnections connections) : IGrantStore
{
    // AUTHZ-CACHE-001: the counter of every account the grant reaches goes up in the
    // same transaction as the grant itself. A group's grant reaches every account the
    // closure holds, at any depth, which is one statement rather than a read and a
    // write per member.
    private const string RaiseForAccount =
        """
        INSERT INTO identity.grant_versions (subject, version)
        VALUES (@subject, 1)
        ON CONFLICT (subject) DO UPDATE SET version = identity.grant_versions.version + 1;
        """;

    private const string RaiseForGroup =
        """
        INSERT INTO identity.grant_versions (subject, version)
        SELECT closure.member_id, 1
        FROM identity.group_closure AS closure
        WHERE closure.group_id = @group AND closure.member_type = 'user'
        ON CONFLICT (subject) DO UPDATE SET version = identity.grant_versions.version + 1;
        """;

    /// <inheritdoc/>
    public async ValueTask<Grant?> FindAsync(GrantId id, CancellationToken cancellationToken)
    {
        GrantRecord? record = await context.Grants
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(Grant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        await context.Grants.AddAsync(Write(grant), cancellationToken).ConfigureAwait(false);
        await RaiseAsync(grant.Subject, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Grant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        GrantRecord record = await context.Grants
            .FirstOrDefaultAsync(row => row.Id == grant.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The grant has no row to carry it onto.");

        record.RevokedBy = grant.RevokedBy;
        record.RevokedAt = grant.RevokedAt;
        record.RevocationReason = grant.RevocationReason;

        await RaiseAsync(grant.Subject, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ExistsAsync(
        Grant grant,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return await context.Grants
            .AnyAsync(
                row => row.SubjectType == grant.Subject.Type
                    && row.SubjectId == grant.Subject.Value
                    && row.Role == grant.Role
                    && row.Organization == grant.Organization
                    && row.ResourceType == grant.ResourceType
                    && row.ResourceId == grant.ResourceId
                    && row.Deny == grant.Deny
                    && row.RevokedAt == null
                    && (row.ExpiresAt == null || row.ExpiresAt > at),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> NamesAsync(RoleName role, CancellationToken cancellationToken) =>
        await context.Grants
            .AnyAsync(row => row.Role == role, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<bool> NamesAsync(GrantSubject holder, CancellationToken cancellationToken) =>
        await context.Grants
            .AnyAsync(row => row.SubjectType == holder.Type && row.SubjectId == holder.Value, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Grant>> HeldByAsync(
        IReadOnlyList<GrantSubject> holders,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(holders);

        Guid[] accounts = [.. Values(holders, SubjectType.User)];
        Guid[] groups = [.. Values(holders, SubjectType.Group)];

        List<GrantRecord> records = await context.Grants
            .Where(row => row.Organization == organization
                && row.RevokedAt == null
                && (row.ExpiresAt == null || row.ExpiresAt > at)
                && ((row.SubjectType == SubjectType.User && accounts.Contains(row.SubjectId))
                    || (row.SubjectType == SubjectType.Group && groups.Contains(row.SubjectId))))
            .OrderBy(row => row.GrantedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(Read)];
    }

    /// <inheritdoc/>
    public async ValueTask<long> VersionAsync(SubjectId subject, CancellationToken cancellationToken) =>
        await context.GrantVersions
            .Where(row => row.Subject == subject)
            .Select(row => row.Version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Grant>> MaterialisedAsync(
        RoleName role,
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        ResourceId?[] records = [.. resources.Select(resource => (ResourceId?)resource)];

        List<GrantRecord> rows = await context.Grants
            .Where(row => row.Kind == GrantKind.Materialised
                && row.Role == role
                && row.Organization == organization
                && row.RevokedAt == null
                && (row.ExpiresAt == null || row.ExpiresAt > at)
                && row.ResourceType == type
                && records.Contains(row.ResourceId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(Read)];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Grant>> OnAsync(
        ResourceReference reference,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // IDN-ORG-003 AC1, entry 196: what the view confers, which leaves out a
        // suspended organization's grants and a role that allows nothing.
        List<GrantRecord> records = await context.Grants
            .Where(row => row.Organization == organization
                && row.RevokedAt == null
                && (row.ExpiresAt == null || row.ExpiresAt > at)
                && context.Organizations.Any(held => held.Id == row.Organization
                    && held.DeletionRequestedAt == null)
                && context.RolePermissions.Any(allowed => allowed.Role == row.Role)
                && (row.ResourceType == null
                    || context.Ancestry.Any(entry => entry.Type == reference.Type
                        && entry.Id == reference.Id
                        && entry.AncestorType == row.ResourceType
                        && entry.AncestorId == row.ResourceId)))
            .OrderBy(row => row.GrantedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(Read)];
    }

    private static IEnumerable<Guid> Values(IReadOnlyList<GrantSubject> holders, SubjectType type) =>
        holders.Where(holder => holder.Type == type).Select(holder => holder.Value);

    private static GrantRecord Write(Grant grant) => new()
    {
        Id = grant.Id,
        SubjectType = grant.Subject.Type,
        SubjectId = grant.Subject.Value,
        Role = grant.Role,
        Organization = grant.Organization,
        ResourceType = grant.ResourceType,
        ResourceId = grant.ResourceId,
        Deny = grant.Deny,
        Kind = grant.Kind,
        ExpiresAt = grant.ExpiresAt,
        GrantedBy = grant.GrantedBy,
        GrantedAt = grant.GrantedAt,
        Reason = grant.Reason,
        RevokedBy = grant.RevokedBy,
        RevokedAt = grant.RevokedAt,
        RevocationReason = grant.RevocationReason,
    };

    private static Grant Read(GrantRecord record) => Grant.Existing(
        record.Id,
        new GrantSubject(record.SubjectType, record.SubjectId),
        record.Role,
        record.Organization,
        record.ResourceType is null
            ? null
            : new ResourceReference(record.ResourceType.Value, record.ResourceId!.Value),
        record.Deny,
        record.Kind,
        record.ExpiresAt,
        record.GrantedBy,
        record.GrantedAt,
        record.Reason,
        record.RevokedBy,
        record.RevokedAt,
        record.RevocationReason);

    private async ValueTask RaiseAsync(GrantSubject subject, CancellationToken cancellationToken)
    {
        // The counter is raised from the statement rather than through the tracker,
        // because a group's grant reaches as many accounts as the closure holds and
        // reading them to write them back would be the same work twice.
        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        CommandDefinition command = subject.Type is SubjectType.User
            ? new CommandDefinition(
                RaiseForAccount,
                new { subject = subject.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken)
            : new CommandDefinition(
                RaiseForGroup,
                new { group = subject.Value },
                ambient.Transaction,
                cancellationToken: cancellationToken);

        await ambient.Connection.ExecuteAsync(command).ConfigureAwait(false);
    }
}
