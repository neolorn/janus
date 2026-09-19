using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Resources;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Resources;

/// <summary>
/// The host's records and their ancestry, over the <c>resources</c> and
/// <c>ancestry</c> tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="connections">Where the ancestry statements take their connection from.</param>
/// <remarks>
/// Implements AUTHZ-INHERIT-001, AUTHZ-INHERIT-002, AUTHZ-INHERIT-003 and
/// CONV-DESIGN-003. The ancestry is written in the same transaction as the create or the
/// move, so permission data and business data cannot diverge. The statements take the
/// change as arguments and read only the ancestry, so they do not depend on when the
/// operation's own row writes reach the database.
/// </remarks>
internal sealed class ResourceStore(JanusDbContext context, DataConnections connections) : IResourceStore
{
    // AUTHZ-INHERIT-003 AC2: a create reads the container's ancestry and a move
    // rewrites a subtree's, so two of them running at once over one tree can leave a
    // row naming a container the record has left. The tree is locked for the rest of
    // the transaction, per organization, which is where a tree begins and ends
    // (AUTHZ-SCOPE-001). Writes to the tree are rare beside reads of it, and no read
    // takes this lock.
    private const string LockTree =
        "SELECT pg_advisory_xact_lock(hashtextextended(CAST(@organization AS text), 0));";

    // AUTHZ-INHERIT-002: a record is its own ancestor at depth zero, and everything
    // above its container is one step further above it.
    private const string Write =
        """
        INSERT INTO janus.ancestry
            (resource_type, resource_id, ancestor_type, ancestor_id, depth, organization)
        SELECT CAST(@type AS text), CAST(@id AS text),
               CAST(@type AS text), CAST(@id AS text), 0, CAST(@organization AS uuid)
        UNION ALL
        SELECT CAST(@type AS text), CAST(@id AS text),
               above.ancestor_type, above.ancestor_id, above.depth + 1,
               CAST(@organization AS uuid)
        FROM janus.ancestry AS above
        WHERE above.resource_type = CAST(@containerType AS text)
          AND above.resource_id = CAST(@containerId AS text)
        ON CONFLICT (resource_type, resource_id, ancestor_type, ancestor_id)
        DO UPDATE SET depth = EXCLUDED.depth;
        """;

    // AUTHZ-INHERIT-002 AC2: everything the record contains, at any depth, stops being
    // beneath what the record used to sit under.
    private const string Detach =
        """
        WITH above AS (
            SELECT entry.ancestor_type, entry.ancestor_id
            FROM janus.ancestry AS entry
            WHERE entry.resource_type = CAST(@type AS text)
              AND entry.resource_id = CAST(@id AS text)
              AND entry.depth > 0
        ),
        beneath AS (
            SELECT entry.resource_type, entry.resource_id
            FROM janus.ancestry AS entry
            WHERE entry.ancestor_type = CAST(@type AS text)
              AND entry.ancestor_id = CAST(@id AS text)
        )
        DELETE FROM janus.ancestry AS target
        USING above, beneath
        WHERE target.resource_type = beneath.resource_type
          AND target.resource_id = beneath.resource_id
          AND target.ancestor_type = above.ancestor_type
          AND target.ancestor_id = above.ancestor_id;
        """;

    // AUTHZ-INHERIT-002 AC2: and comes to sit under the new container and everything
    // above it, each descendant keeping its own remove from the record that moved.
    private const string Attach =
        """
        INSERT INTO janus.ancestry
            (resource_type, resource_id, ancestor_type, ancestor_id, depth, organization)
        SELECT beneath.resource_type, beneath.resource_id,
               above.ancestor_type, above.ancestor_id,
               beneath.depth + 1 + above.depth,
               beneath.organization
        FROM janus.ancestry AS beneath
        CROSS JOIN janus.ancestry AS above
        WHERE beneath.ancestor_type = CAST(@type AS text)
          AND beneath.ancestor_id = CAST(@id AS text)
          AND above.resource_type = CAST(@containerType AS text)
          AND above.resource_id = CAST(@containerId AS text)
        ON CONFLICT (resource_type, resource_id, ancestor_type, ancestor_id)
        DO UPDATE SET depth = EXCLUDED.depth;
        """;

    /// <inheritdoc/>
    public async ValueTask<RegisteredResource?> FindAsync(
        ResourceReference reference,
        CancellationToken cancellationToken)
    {
        ResourceRecord? record = await context.Resources
            .FirstOrDefaultAsync(
                row => row.Type == reference.Type && row.Id == reference.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        return RegisteredResource.Existing(
            new ResourceReference(record.Type, record.Id),
            record.Organization,
            record.ContainedInType is null
                ? null
                : new ResourceReference(record.ContainedInType.Value, record.ContainedInId!.Value));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(
        RegisteredResource resource,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);

        await context.Resources.AddAsync(Row(resource), cancellationToken).ConfigureAwait(false);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);
        object arguments = Arguments(resource);

        await RunAsync(ambient, LockTree, arguments, cancellationToken).ConfigureAwait(false);
        await RunAsync(ambient, Write, arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RegisterManyAsync(
        IReadOnlyList<RegisteredResource> resources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        await context.Resources
            .AddRangeAsync(resources.Select(Row), cancellationToken)
            .ConfigureAwait(false);

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        // Containers come before their contents, so each record's container already has
        // the ancestry this statement reads.
        foreach (RegisteredResource resource in resources)
        {
            object arguments = Arguments(resource);

            await RunAsync(ambient, LockTree, arguments, cancellationToken).ConfigureAwait(false);
            await RunAsync(ambient, Write, arguments, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask MoveAsync(
        ResourceReference reference,
        ResourceReference? containedIn,
        CancellationToken cancellationToken)
    {
        ResourceRecord record = await context.Resources
            .FirstOrDefaultAsync(
                row => row.Type == reference.Type && row.Id == reference.Id,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The record has no row to move.");

        record.ContainedInType = containedIn?.Type;
        record.ContainedInId = containedIn?.Id;

        object arguments = new
        {
            type = reference.Type.ToString(),
            id = reference.Id.ToString(),
            organization = record.Organization.Value,
            containerType = containedIn?.Type.ToString(),
            containerId = containedIn?.Id.ToString(),
        };

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        await RunAsync(ambient, LockTree, arguments, cancellationToken).ConfigureAwait(false);
        await RunAsync(ambient, Detach, arguments, cancellationToken).ConfigureAwait(false);
        await RunAsync(ambient, Attach, arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ResourceReference>> AncestryAsync(
        ResourceReference reference,
        CancellationToken cancellationToken)
    {
        List<AncestryRecord> records = await context.Ancestry
            .Where(entry => entry.Type == reference.Type && entry.Id == reference.Id)
            .OrderBy(entry => entry.Depth)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(entry => new ResourceReference(entry.AncestorType, entry.AncestorId))];
    }

    private static ResourceRecord Row(RegisteredResource resource) => new()
    {
        Type = resource.Reference.Type,
        Id = resource.Reference.Id,
        Organization = resource.Organization,
        ContainedInType = resource.ContainedIn?.Type,
        ContainedInId = resource.ContainedIn?.Id,
    };

    private static object Arguments(RegisteredResource resource) => new
    {
        type = resource.Reference.Type.ToString(),
        id = resource.Reference.Id.ToString(),
        organization = resource.Organization.Value,
        containerType = resource.ContainedIn?.Type.ToString(),
        containerId = resource.ContainedIn?.Id.ToString(),
    };

    private static async Task RunAsync(
        AmbientConnection ambient,
        string statement,
        object arguments,
        CancellationToken cancellationToken) =>
        await ambient.Connection
            .ExecuteAsync(new CommandDefinition(
                statement,
                arguments,
                ambient.Transaction,
                cancellationToken: cancellationToken))
            .ConfigureAwait(false);
}
