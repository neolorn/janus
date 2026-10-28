using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Authentication.Tests.Records;

/// <summary>
/// The offers to attach, held in memory as the table holds them: each read is a copy of
/// what was last written.
/// </summary>
internal sealed class RecordOfferStoreInMemory : IRecordOfferStore
{
    private readonly Dictionary<SessionId, OfferToAttach> _offers = [];

    /// <summary>
    /// Every offer the store holds, as last written.
    /// </summary>
    public IReadOnlyCollection<OfferToAttach> All => [.. _offers.Values.Select(Copied)];

    /// <inheritdoc/>
    public ValueTask<OfferToAttach?> FindAsync(SessionId session, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_offers.TryGetValue(session, out OfferToAttach? held) ? Copied(held) : null);

    /// <inheritdoc/>
    public ValueTask<OfferToAttach?> FindForUpdateAsync(SessionId session, CancellationToken cancellationToken) =>
        FindAsync(session, cancellationToken);

    /// <inheritdoc/>
    public ValueTask PutAsync(OfferToAttach offer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(offer);

        _offers[offer.Session] = Copied(offer);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(SessionId session, CancellationToken cancellationToken)
    {
        _ = _offers.Remove(session);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<SessionId> gone = [.. _offers
            .Where(offer => offer.Value.HasExpired(now))
            .Select(offer => offer.Key)];

        foreach (SessionId session in gone)
        {
            _ = _offers.Remove(session);
        }

        return ValueTask.FromResult(gone.Count);
    }

    private static OfferToAttach Copied(OfferToAttach offer) =>
        OfferToAttach.Existing(
            offer.Session,
            [.. offer.Proven.Select(value => value with { Fingerprints = [.. value.Fingerprints] })],
            [.. offer.Offered],
            offer.ExpiresAt);
}
