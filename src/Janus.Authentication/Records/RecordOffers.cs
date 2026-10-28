using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Records;

/// <summary>
/// What a proof of an account's emails or phones offers it: the person records holding
/// a value proved now as a contact verified at a collection.
/// </summary>
/// <param name="offers">Where each session's offer is held.</param>
/// <param name="records">The person records a proof finds.</param>
/// <param name="configuration">Where the offer's life and the age of a proof are read.</param>
/// <param name="time">The deployment's clock.</param>
/// <remarks>
/// Implements REC-ATT-001, REG-IDENT-004, REG-IDENT-007 and REG-SESS-007. A proof is a
/// verification that completes now; an identifier verified before proves nothing, so
/// this is asked where a verification completes and nowhere else. It writes in its
/// caller's unit of work and begins none.
/// </remarks>
internal sealed class RecordOffers(
    IRecordOfferStore offers,
    IRecordDirectory records,
    IConfigurationStore configuration,
    TimeProvider time)
{
    /// <summary>
    /// Makes the offer a proof brings on the session the proof completed under, in place
    /// of the one it held.
    /// </summary>
    /// <param name="session">The account's session.</param>
    /// <param name="proved">The values proved now, each by its kind and canonical form.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Nothing, or the failure the configuration produced.</returns>
    /// <exception cref="ArgumentNullException">The values are absent.</exception>
    public async ValueTask<Error?> ProvedAsync(
        SessionId session,
        IReadOnlyList<(IdentifierKind Kind, string Canonical)> proved,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proved);

        Error? failure = null;

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.RecordsAccessLifetime, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<TimeSpan>(error, ref failure));

        TimeSpan proofAge = (await configuration
                .ReadAsync(Settings.RecordsProofAge, cancellationToken).ConfigureAwait(false))
            .Match(found => found, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return failure;
        }

        DateTimeOffset now = time.GetUtcNow();

        ProvenValue[] proven =
        [
            .. proved
                .Where(value => value.Kind is IdentifierKind.Email or IdentifierKind.Phone)
                .Select(value => new ProvenValue(value.Kind, records.FingerprintsOf(value.Canonical))),
        ];

        var held = new List<SubjectId>();
        var offered = new List<SubjectId>();

        foreach (ProvenValue value in proven)
        {
            held.AddRange(await ActiveAsync(value, DateTimeOffset.MinValue, cancellationToken).ConfigureAwait(false));
            offered.AddRange(await ActiveAsync(value, now - proofAge, cancellationToken).ConfigureAwait(false));
        }

        // REC-ATT-001: the offer is what the latest proof offers, so a proof no record
        // answers leaves none standing. One that some record answers stands for its
        // life, whatever the age of that record's verification, since such a record is
        // attached by its reference inside the offer's life.
        if (held.Count == 0)
        {
            await offers.RemoveAsync(session, cancellationToken).ConfigureAwait(false);

            return null;
        }

        await offers
            .PutAsync(OfferToAttach.Made(session, proven, offered, now, lifetime), cancellationToken)
            .ConfigureAwait(false);

        return null;
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // REC-ATT-001: an offer counts the active records attached to no account that hold
    // the value as a contact last verified at a collection at or after the instant.
    private async ValueTask<IEnumerable<SubjectId>> ActiveAsync(
        ProvenValue value,
        DateTimeOffset verifiedSince,
        CancellationToken cancellationToken) =>
        (await records
            .ReachedAsync(value.Kind, value.Fingerprints, verifiedSince, cancellationToken)
            .ConfigureAwait(false))
        .Where(record => record.State is PersonRecordState.Active)
        .Select(record => record.Subject);
}
