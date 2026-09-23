using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Groups;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// Groups, over the <c>groups</c>, <c>group_members</c> and <c>group_closure</c>
/// tables.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <param name="connections">Where the closure statements take their connection from.</param>
/// <remarks>
/// Implements AUTHZ-GROUP-001, AUTHZ-GROUP-002, AUTHZ-CACHE-001 and CONV-DESIGN-003.
/// A membership change rewrites the closure of the groups it can reach and raises the
/// counter of every account beneath them, both in the same transaction as the change, so
/// no principal keeps an entry the change has invalidated. The statements take the
/// changed edge as an argument rather than reading it back, so they do not depend on
/// when the operation's own writes reach the database.
/// </remarks>
internal sealed class GroupStore(StoreContext context, DataConnections connections) : IGroupStore
{
    // The edges as they stand once the change is in: every stored membership but the
    // one being removed, and the one being added.
    private const string Edges =
        """
        edges AS (
            SELECT member.group_id, member.member_type, member.member_id
            FROM identity.group_members AS member
            WHERE NOT (member.group_id = CAST(@group AS uuid)
                AND member.member_type = CAST(@memberType AS text)
                AND member.member_id = CAST(@member AS uuid))
            UNION ALL
            SELECT CAST(@group AS uuid), CAST(@memberType AS text), CAST(@member AS uuid)
            WHERE CAST(@holds AS boolean)
        )
        """;

    private const string Clear =
        "DELETE FROM identity.group_closure WHERE group_id = ANY(CAST(@affected AS uuid[]));";

    // AUTHZ-GROUP-001, AUTHZ-INHERIT-002 AC4: the closure is rewritten here, by a
    // recursion that runs once per membership change, so that the question "which
    // groups hold this principal" stays one indexed read.
    private const string Rebuild =
        "WITH RECURSIVE " + Edges +
        """
        ,
        reach AS (
            SELECT root.group_id AS root, edges.member_type, edges.member_id, 1 AS depth
            FROM unnest(CAST(@affected AS uuid[])) AS root(group_id)
            JOIN edges ON edges.group_id = root.group_id
            UNION ALL
            SELECT reach.root, edges.member_type, edges.member_id, reach.depth + 1
            FROM reach
            JOIN edges ON edges.group_id = reach.member_id
            WHERE reach.member_type = 'group' AND reach.depth < 64
        )
        INSERT INTO identity.group_closure (group_id, member_type, member_id, depth)
        SELECT root, member_type, member_id, MIN(depth)
        FROM reach
        GROUP BY root, member_type, member_id;
        """;

    // AUTHZ-CACHE-001 AC3: every account the change can reach, whichever side of it the
    // account sits on. Running this before and after the rewrite covers what was
    // reachable and what now is.
    private const string Raise =
        """
        WITH reached AS (
            SELECT closure.member_id AS subject
            FROM identity.group_closure AS closure
            WHERE closure.member_type = 'user'
              AND closure.group_id = ANY(CAST(@affected AS uuid[]))
            UNION
            SELECT CAST(@member AS uuid) WHERE CAST(@memberType AS text) = 'user'
            UNION
            SELECT closure.member_id
            FROM identity.group_closure AS closure
            WHERE closure.member_type = 'user'
              AND CAST(@memberType AS text) = 'group'
              AND closure.group_id = CAST(@member AS uuid)
        )
        INSERT INTO identity.grant_versions (subject, version)
        SELECT reached.subject, 1
        FROM reached
        WHERE EXISTS (
            SELECT 1 FROM identity.accounts AS account WHERE account.subject = reached.subject)
        ON CONFLICT (subject) DO UPDATE SET version = identity.grant_versions.version + 1;
        """;

    /// <inheritdoc/>
    public async ValueTask<Group?> FindAsync(GroupId id, CancellationToken cancellationToken)
    {
        GroupRecord? record = await context.Groups
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Group.Existing(record.Id, record.Organization, record.Name);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(Group group, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);

        await context.Groups
            .AddAsync(
                new GroupRecord
                {
                    Id = group.Id,
                    Organization = group.Organization,
                    Name = group.Name,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask AddMemberAsync(
        GroupId group,
        GrantSubject member,
        CancellationToken cancellationToken)
    {
        await context.GroupMembers
            .AddAsync(
                new GroupMemberRecord
                {
                    Group = group,
                    MemberType = member.Type,
                    MemberId = member.Value,
                },
                cancellationToken)
            .ConfigureAwait(false);

        await CarryAsync(group, member, holds: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RemoveMemberAsync(
        GroupId group,
        GrantSubject member,
        CancellationToken cancellationToken)
    {
        GroupMemberRecord? record = await context.GroupMembers
            .FirstOrDefaultAsync(
                row => row.Group == group
                    && row.MemberType == member.Type
                    && row.MemberId == member.Value,
                cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.GroupMembers.Remove(record);
        }

        await CarryAsync(group, member, holds: false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<GrantSubject>> MembersAsync(
        GroupId group,
        CancellationToken cancellationToken)
    {
        List<GroupMemberRecord> records = await context.GroupMembers
            .Where(row => row.Group == group)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. records.Select(row => new GrantSubject(row.MemberType, row.MemberId))];
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<GroupId>> GroupsOfAsync(
        GrantSubject subject,
        CancellationToken cancellationToken)
    {
        List<GroupId> groups = await context.GroupClosure
            .Where(row => row.MemberType == subject.Type && row.MemberId == subject.Value)
            .Select(row => row.Group)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return groups;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ReachesAsync(
        GroupId group,
        GrantSubject member,
        CancellationToken cancellationToken) =>
        await context.GroupClosure
            .AnyAsync(
                row => row.Group == group
                    && row.MemberType == member.Type
                    && row.MemberId == member.Value,
                cancellationToken)
            .ConfigureAwait(false);

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

    private async ValueTask CarryAsync(
        GroupId group,
        GrantSubject member,
        bool holds,
        CancellationToken cancellationToken)
    {
        // The groups whose reachability the change can alter: this one and every group
        // holding it. Nothing above them is reached differently, so nothing above them
        // is rewritten. It is read before the rows are cleared, because clearing them
        // is what loses the answer.
        Guid[] above = await context.GroupClosure
            .Where(row => row.MemberType == SubjectType.Group && row.MemberId == group.Value)
            .Select(row => row.Group.Value)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        object arguments = new
        {
            group = group.Value,
            member = member.Value,
            memberType = member.Type is SubjectType.User ? "user" : "group",
            affected = (Guid[])[group.Value, .. above],
            holds,
        };

        AmbientConnection ambient = await connections.UseAsync(cancellationToken).ConfigureAwait(false);

        await RunAsync(ambient, Raise, arguments, cancellationToken).ConfigureAwait(false);
        await RunAsync(ambient, Clear, arguments, cancellationToken).ConfigureAwait(false);
        await RunAsync(ambient, Rebuild, arguments, cancellationToken).ConfigureAwait(false);
        await RunAsync(ambient, Raise, arguments, cancellationToken).ConfigureAwait(false);
    }
}
