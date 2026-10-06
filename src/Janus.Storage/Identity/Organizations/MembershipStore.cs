using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Storage.Authentication.Invitations;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Identity.Organizations;

/// <summary>
/// Memberships, over the <c>memberships</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>
/// Implements IDN-MEM-001, IDN-MEM-002 and CONV-DESIGN-003. A read returns the
/// memberships that ended beside the ones that have not, because a membership is a
/// record of something that happened (IDN-PRIN-003). The acknowledgement an invitation
/// attached is written with the membership and never changed (REG-INV-001).
/// </remarks>
internal sealed class MembershipStore(StoreContext context) : IMembershipStore
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Membership>> FindBySubjectAsync(
        SubjectId subject,
        CancellationToken cancellationToken)
    {
        List<MembershipRecord> records = await context.Memberships
            .Where(membership => membership.Subject == subject)
            .OrderBy(membership => membership.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Read(records);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<IReadOnlyList<Membership>> FindBySubjectForUpdateAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        await HeldAsync(
                context.Memberships.FromSql(
                    $"SELECT * FROM identity.memberships WHERE subject = {subject.Value} ORDER BY id FOR UPDATE"),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<IReadOnlyList<Membership>> FindByOrganizationForUpdateAsync(
        OrganizationId organization,
        CancellationToken cancellationToken) =>
        await HeldAsync(
                context.Memberships.FromSql(
                    $"SELECT * FROM identity.memberships WHERE organization = {organization.Value} ORDER BY id FOR UPDATE"),
                cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Membership>> FindByOrganizationAsync(
        OrganizationId organization,
        CancellationToken cancellationToken)
    {
        List<MembershipRecord> records = await context.Memberships
            .Where(membership => membership.Organization == organization)
            .OrderBy(membership => membership.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Read(records);
    }

    /// <inheritdoc/>
    public async ValueTask CreateAsync(Membership membership, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(membership);

        await context.Memberships
            .AddAsync(
                new MembershipRecord
                {
                    Id = membership.Id,
                    Subject = membership.Subject,
                    Organization = membership.Organization,
                    CreatedAt = membership.CreatedAt,
                    EndedAt = membership.EndedAt,
                    AcknowledgedDocuments = membership.Acknowledgement is MembershipAcknowledgement acknowledged
                        ? JsonSerializer.Serialize(
                            (IReadOnlyList<InvitedDocument>)
                            [
                                .. acknowledged.Documents.Select(document =>
                                    new InvitedDocument(document.Document, document.Version)),
                            ],
                            InvitationJson.Default.IReadOnlyListInvitedDocument)
                        : null,
                    AcknowledgedAt = membership.Acknowledgement?.At,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask RecordAsync(Membership membership, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(membership);

        MembershipRecord record = await context.Memberships
            .FindAsync([membership.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The membership has no row to carry the change.");

        record.EndedAt = membership.EndedAt;
    }

    // The rows are locked in one order, so two transactions locking overlapping sets
    // wait for each other rather than each holding a part. A row the context already
    // tracks was read before the lock, so it is read again.
    private async ValueTask<IReadOnlyList<Membership>> HeldAsync(
        IQueryable<MembershipRecord> locking,
        CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("A membership's row is held only inside the operation's transaction.");
        }

        HashSet<MembershipId> tracked = [.. context.Memberships.Local.Select(record => record.Id)];

        List<MembershipRecord> held = await locking.ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (MembershipRecord record in held.Where(record => tracked.Contains(record.Id)))
        {
            await context.Entry(record).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return Read([.. held.OrderBy(record => record.CreatedAt)]);
    }

    private static List<Membership> Read(List<MembershipRecord> records)
    {
        var memberships = new List<Membership>(records.Count);

        foreach (MembershipRecord record in records)
        {
            memberships.Add(Membership.Existing(
                record.Id,
                record.Subject,
                record.Organization,
                record.CreatedAt,
                record.EndedAt,
                Acknowledged(record)));
        }

        return memberships;
    }

    private static MembershipAcknowledgement? Acknowledged(MembershipRecord record)
    {
        if (record.AcknowledgedAt is not DateTimeOffset at)
        {
            return null;
        }

        IReadOnlyList<InvitedDocument> documents = JsonSerializer.Deserialize(
                record.AcknowledgedDocuments
                ?? throw new InvalidOperationException("The acknowledgement has no documents."),
                InvitationJson.Default.IReadOnlyListInvitedDocument)
            ?? throw new InvalidOperationException("The acknowledged documents are not a list.");

        return new MembershipAcknowledgement(
            [.. documents.Select(document => new InvitationDocument(document.Document, document.Version))],
            at);
    }
}
