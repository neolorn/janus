using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// The offers to attach each account session holds, over the <c>record_offers</c> table.
/// </summary>
/// <param name="context">The context the operation's writes are tracked on.</param>
/// <remarks>Implements REC-ATT-001 and CONV-DESIGN-003.</remarks>
internal sealed class RecordOfferStore(StoreContext context) : IRecordOfferStore
{
    /// <inheritdoc/>
    public async ValueTask<OfferToAttach?> FindAsync(SessionId session, CancellationToken cancellationToken)
    {
        RecordOfferRecord? record = await context.RecordOffers
            .FindAsync([session], cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No transaction is open.</exception>
    public async ValueTask<OfferToAttach?> FindForUpdateAsync(SessionId session, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("An offer's row is held only inside the operation's transaction.");
        }

        bool tracked = context.RecordOffers.Local.Any(record => record.SessionId == session);

        RecordOfferRecord? held = (await context.RecordOffers
                .FromSql($"SELECT * FROM identity.record_offers WHERE session_id = {session.Value} FOR UPDATE")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .SingleOrDefault();

        // A row the context already tracks was read before the lock, so it is read again:
        // what the decision is made on is the row as it stood when the lock was taken.
        if (held is not null && tracked)
        {
            await context.Entry(held).ReloadAsync(cancellationToken).ConfigureAwait(false);
        }

        return held is null ? null : Read(held);
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(OfferToAttach offer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(offer);

        RecordOfferRecord? record = await context.RecordOffers
            .FindAsync([offer.Session], cancellationToken)
            .ConfigureAwait(false);

        if (record is null)
        {
            record = new RecordOfferRecord { SessionId = offer.Session };

            await context.RecordOffers.AddAsync(record, cancellationToken).ConfigureAwait(false);
        }

        record.Proven = JsonSerializer.Serialize<IReadOnlyList<ProvenValueDocument>>(
            [
                .. offer.Proven.Select(value => new ProvenValueDocument(
                    VocabularyConverter<IdentifierKind>.Write(value.Kind),
                    value.Fingerprints)),
            ],
            RecordOfferJson.Default.IReadOnlyListProvenValueDocument);
        record.Offered = [.. offer.Offered.Select(subject => subject.Value)];
        record.ExpiresAt = offer.ExpiresAt;
    }

    /// <inheritdoc/>
    public async ValueTask RemoveAsync(SessionId session, CancellationToken cancellationToken)
    {
        RecordOfferRecord? record = await context.RecordOffers
            .FindAsync([session], cancellationToken)
            .ConfigureAwait(false);

        if (record is not null)
        {
            context.RecordOffers.Remove(record);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.RecordOffers
            .Where(offer => offer.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static OfferToAttach Read(RecordOfferRecord record) =>
        OfferToAttach.Existing(
            record.SessionId,
            [
                .. (JsonSerializer.Deserialize(record.Proven, RecordOfferJson.Default.IReadOnlyListProvenValueDocument)
                        ?? throw new InvalidOperationException("An offer's proven values do not read."))
                    .Select(value => new ProvenValue(
                        VocabularyConverter<IdentifierKind>.Read(value.Kind),
                        value.Fingerprints)),
            ],
            [.. record.Offered.Select(subject => new SubjectId(subject))],
            record.ExpiresAt);
}
