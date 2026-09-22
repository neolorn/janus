using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Consents;

namespace Janus.Privacy.Tests.Consents;

/// <summary>
/// The consent and objection records, one a subject and purpose, in the order they
/// were first decided.
/// </summary>
internal sealed class ConsentStoreInMemory : IConsentStore
{
    private readonly Dictionary<(SubjectId Subject, string Purpose), ConsentRecord> _consents =
        [];

    private readonly Dictionary<(SubjectId Subject, string Purpose), ObjectionRecord> _objections =
        [];

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ConsentRecord>> ConsentsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ConsentRecord>>(
        [
            .. _consents
                .Where(held => held.Key.Subject == subject)
                .Select(held => held.Value)
                .OrderBy(record => record.GrantedAt),
        ]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ObjectionRecord>> ObjectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ObjectionRecord>>(
        [
            .. _objections
                .Where(held => held.Key.Subject == subject)
                .Select(held => held.Value)
                .OrderBy(record => record.RecordedAt),
        ]);

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        SubjectId subject,
        ConsentRecord consent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consent);

        _consents[(subject, consent.Purpose)] = consent;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(
        SubjectId subject,
        ObjectionRecord objection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objection);

        _objections[(subject, objection.Purpose)] = objection;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<HeldConsent>> LiveAgainstAnotherAsync(
        IReadOnlyCollection<string> purposes,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(purposes);

        return ValueTask.FromResult<IReadOnlyList<HeldConsent>>(
        [
            .. _consents
                .Where(held => held.Value.Live
                    && purposes.Contains(held.Value.Purpose)
                    && !string.Equals(held.Value.NoticeVersion, version, StringComparison.Ordinal))
                .Select(held => new HeldConsent(held.Key.Subject, held.Value))
                .OrderBy(one => one.Consent.GrantedAt),
        ]);
    }
}
