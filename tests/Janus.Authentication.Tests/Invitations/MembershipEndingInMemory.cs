using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Authentication.Tests.Policies;
using Janus.Core;

namespace Janus.Authentication.Tests.Invitations;

/// <summary>
/// The memberships administrators ended, and the ends they set in advance, held in memory.
/// </summary>
/// <param name="lookup">Where the membership is taken out of, so that the policy no longer resolves through it.</param>
/// <remarks>
/// CONV-TEST-004: a fake that ends only a membership the account holds, as the rows
/// do, and keeps what it ended.
/// </remarks>
internal sealed class MembershipEndingInMemory(MembershipLookupInMemory lookup) : IMembershipEnding
{
    private readonly Dictionary<(SubjectId Subject, OrganizationId Organization), SetEnd> _set = [];

    /// <summary>
    /// Every membership ended, oldest first.
    /// </summary>
    public List<EndedMembership> Ended { get; } = [];

    /// <summary>
    /// What happens while an end waits for the membership's row, where a test sets it.
    /// </summary>
    public Action? Ending { get; set; }

    /// <inheritdoc/>
    public async ValueTask<MembershipId?> FindAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await lookup.OfAsync(subject, cancellationToken).ConfigureAwait(false)).Contains(organization)
            ? new MembershipId(Guid.CreateVersion7())
            : null;

    /// <inheritdoc/>
    public ValueTask<MembershipId?> EndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Ending?.Invoke();

        if (!lookup.Leave(subject, organization))
        {
            return ValueTask.FromResult<MembershipId?>(null);
        }

        var membership = new MembershipId(Guid.CreateVersion7(at));

        _ = _set.Remove((subject, organization));
        Ended.Add(new EndedMembership(membership, subject, organization, at));

        return ValueTask.FromResult<MembershipId?>(membership);
    }

    /// <summary>
    /// The end set on the account's membership of the organization, where one is.
    /// </summary>
    /// <param name="subject">Whose membership.</param>
    /// <param name="organization">Of which organization.</param>
    /// <returns>The instant, the administrator who set it and why, or nothing.</returns>
    public SetEnd? EndSetOn(SubjectId subject, OrganizationId organization) =>
        _set.TryGetValue((subject, organization), out SetEnd? set) ? set : null;

    /// <inheritdoc/>
    public async ValueTask<DateTimeOffset?> EndSetAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await FindAsync(subject, organization, cancellationToken).ConfigureAwait(false) is null
            ? null
            : EndSetOn(subject, organization)?.At;

    /// <inheritdoc/>
    public async ValueTask<MembershipId?> SetEndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset endsAt,
        SubjectId setBy,
        string reason,
        CancellationToken cancellationToken)
    {
        Ending?.Invoke();

        if (await FindAsync(subject, organization, cancellationToken).ConfigureAwait(false) is not MembershipId held)
        {
            return null;
        }

        _set[(subject, organization)] = new SetEnd(endsAt, setBy, reason);

        return held;
    }

    /// <inheritdoc/>
    public async ValueTask<MembershipId?> CancelEndAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        Ending?.Invoke();

        return await FindAsync(subject, organization, cancellationToken).ConfigureAwait(false) is MembershipId held
            && _set.Remove((subject, organization))
            ? held
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<MembershipEndDue>> DueAsync(
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        List<MembershipEndDue> due = [];

        foreach (((SubjectId subject, OrganizationId organization), SetEnd set) in _set
            .Where(entry => entry.Value.At <= at)
            .OrderBy(entry => entry.Value.At))
        {
            if (await FindAsync(subject, organization, cancellationToken).ConfigureAwait(false) is MembershipId held)
            {
                due.Add(new MembershipEndDue(held, subject, organization, set.SetBy, set.Reason));
            }
        }

        return due;
    }

    /// <inheritdoc/>
    public ValueTask<MembershipEndDue?> EndDueAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        Ending?.Invoke();

        if (EndSetOn(subject, organization) is not { } set || set.At > at || !lookup.Leave(subject, organization))
        {
            return ValueTask.FromResult<MembershipEndDue?>(null);
        }

        var membership = new MembershipId(Guid.CreateVersion7(at));

        _ = _set.Remove((subject, organization));
        Ended.Add(new EndedMembership(membership, subject, organization, at));

        return ValueTask.FromResult<MembershipEndDue?>(
            new MembershipEndDue(membership, subject, organization, set.SetBy, set.Reason));
    }

    /// <summary>
    /// An end set in advance.
    /// </summary>
    /// <param name="At">The instant the membership ends.</param>
    /// <param name="SetBy">The administrator who set it.</param>
    /// <param name="Reason">Why.</param>
    internal sealed record SetEnd(DateTimeOffset At, SubjectId SetBy, string Reason);
}
