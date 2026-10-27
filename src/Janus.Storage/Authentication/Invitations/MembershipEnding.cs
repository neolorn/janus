using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Invitations;
using Janus.Core;
using Janus.Identity.Organizations;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// The membership an administrator ends, or sets to end, over the <c>memberships</c> table.
/// </summary>
/// <param name="memberships">Where the account's memberships are read and the ended one recorded.</param>
/// <remarks>
/// Implements IDN-MEM-001 and CONV-LAYOUT-001 (D-213). The end is the membership's own
/// transition, so the row stays and carries when it ended; an end set in advance is carried
/// on the row until it is moved or cancelled.
/// </remarks>
internal sealed class MembershipEnding(IMembershipStore memberships) : IMembershipEnding
{
    /// <inheritdoc/>
    public async ValueTask<MembershipId?> FindAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await CurrentAsync(subject, organization, cancellationToken).ConfigureAwait(false))?.Id;

    /// <inheritdoc/>
    public async ValueTask<MembershipId?> EndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // D-166 X3: the membership is read under its row's lock, so a second end, or the
        // organization's erasure, at the same moment finds it ended and ends nothing.
        if (await HeldAsync(subject, organization, cancellationToken).ConfigureAwait(false) is not Membership current)
        {
            return null;
        }

        current.End(at);

        await memberships.RecordAsync(current, cancellationToken).ConfigureAwait(false);

        return current.Id;
    }

    /// <inheritdoc/>
    public async ValueTask<DateTimeOffset?> EndSetAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await CurrentAsync(subject, organization, cancellationToken).ConfigureAwait(false))?.EndSet?.At;

    /// <inheritdoc/>
    public async ValueTask<MembershipId?> SetEndAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset endsAt,
        SubjectId setBy,
        string reason,
        CancellationToken cancellationToken)
    {
        // D-166 X3: as for an end now, so an end at the same moment finds the membership
        // ended and sets nothing.
        if (await HeldAsync(subject, organization, cancellationToken).ConfigureAwait(false) is not Membership current)
        {
            return null;
        }

        current.SetEnd(new MembershipEndSet(endsAt, setBy, reason));

        await memberships.RecordAsync(current, cancellationToken).ConfigureAwait(false);

        return current.Id;
    }

    /// <inheritdoc/>
    public async ValueTask<MembershipId?> CancelEndAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        if (await HeldAsync(subject, organization, cancellationToken).ConfigureAwait(false) is not Membership current
            || !current.CancelEnd())
        {
            return null;
        }

        await memberships.RecordAsync(current, cancellationToken).ConfigureAwait(false);

        return current.Id;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<MembershipEndDue>> DueAsync(
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        [
            .. (await memberships.FindEndsDueAsync(at, cancellationToken).ConfigureAwait(false))
                .Select(Due)
                .OfType<MembershipEndDue>(),
        ];

    /// <inheritdoc/>
    public async ValueTask<MembershipEndDue?> EndDueAsync(
        SubjectId subject,
        OrganizationId organization,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        // D-166 X3: the end is read again under the row's lock, so one moved or cancelled
        // since the pass read its list is carried out as it now stands.
        if (await HeldAsync(subject, organization, cancellationToken).ConfigureAwait(false) is not Membership current
            || current.EndSet is not { } set
            || set.At > at)
        {
            return null;
        }

        current.End(at);

        await memberships.RecordAsync(current, cancellationToken).ConfigureAwait(false);

        return Due(current);
    }

    private static MembershipEndDue? Due(Membership membership) =>
        membership.EndSet is { } set
            ? new MembershipEndDue(membership.Id, membership.Subject, membership.Organization, set.SetBy, set.Reason)
            : null;

    private async ValueTask<Membership?> HeldAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await memberships.FindBySubjectForUpdateAsync(subject, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(membership => membership.IsCurrent && membership.Organization == organization);

    private async ValueTask<Membership?> CurrentAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        (await memberships.FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false))
            .SingleOrDefault(membership => membership.IsCurrent && membership.Organization == organization);
}
