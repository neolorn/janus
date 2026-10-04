using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Consents;

namespace Janus.Privacy.Tests.Consents;

/// <summary>
/// The consent and objection records, a record a grant and a record an objection, in
/// the order they were added, with at most one live record a subject and purpose.
/// </summary>
internal sealed class ConsentStoreInMemory : IConsentStore
{
    private readonly List<(SubjectId Subject, ConsentRecord Record)> _consents = [];

    private readonly List<(SubjectId Subject, ObjectionRecord Record)> _objections = [];

    /// <summary>
    /// Gets or sets what another transaction commits while this one waits for the
    /// subject's records, so a test may change one under a decision already made.
    /// </summary>
    public Action? Holding { get; set; }

    /// <summary>
    /// Gets or sets what another transaction commits between this one's read and its
    /// addition, so a test may write a record under a decision already made.
    /// </summary>
    public Action? Adding { get; set; }

    /// <summary>
    /// Keeps a record as one written earlier, in whatever state it is given.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="consent">The record.</param>
    public void Keep(SubjectId subject, ConsentRecord consent) => _consents.Add((subject, consent));

    /// <inheritdoc/>
    public ValueTask HoldAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        Holding?.Invoke();

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ConsentRecord>> ConsentsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ConsentRecord>>(
        [
            .. _consents
                .Where(held => held.Subject == subject)
                .Select(held => held.Record)
                .OrderBy(record => record.GrantedAt),
        ]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ObjectionRecord>> ObjectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ObjectionRecord>>(
        [
            .. _objections
                .Where(held => held.Subject == subject)
                .Select(held => held.Record)
                .OrderBy(record => record.RecordedAt),
        ]);

    /// <inheritdoc/>
    public ValueTask<bool> AddAsync(
        SubjectId subject,
        ConsentRecord consent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consent);

        if (!consent.Live)
        {
            throw new ArgumentException("A consent is added live.", nameof(consent));
        }

        Action? meanwhile = Adding;

        Adding = null;
        meanwhile?.Invoke();

        if (_consents.Exists(held => held.Subject == subject && held.Record.Live && Same(held.Record.Purpose, consent.Purpose)))
        {
            return ValueTask.FromResult(false);
        }

        _consents.Add((subject, consent));

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> AddAsync(
        SubjectId subject,
        ObjectionRecord objection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objection);

        if (!objection.Standing)
        {
            throw new ArgumentException("An objection is added standing.", nameof(objection));
        }

        Action? meanwhile = Adding;

        Adding = null;
        meanwhile?.Invoke();

        if (_objections.Exists(held => held.Subject == subject && held.Record.Standing && Same(held.Record.Purpose, objection.Purpose)))
        {
            return ValueTask.FromResult(false);
        }

        _objections.Add((subject, objection));

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> WithdrawConsentAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        int standing = _consents.FindLastIndex(held => held.Subject == subject && held.Record.Live && Same(held.Record.Purpose, purpose));

        if (standing < 0)
        {
            standing = _consents.FindLastIndex(held => held.Subject == subject && Same(held.Record.Purpose, purpose));
        }

        if (standing < 0 || _consents[standing].Record.WithdrawnAt is not null)
        {
            return ValueTask.FromResult(false);
        }

        _consents[standing] = (subject, _consents[standing].Record with { WithdrawnAt = at });

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> SupersedeAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        int live = _consents.FindIndex(held => held.Subject == subject && held.Record.Live && Same(held.Record.Purpose, purpose));

        if (live < 0)
        {
            return ValueTask.FromResult(false);
        }

        _consents[live] = (subject, _consents[live].Record with { SupersededAt = at });

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<bool> WithdrawObjectionAsync(
        SubjectId subject,
        string purpose,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        int standing = _objections.FindIndex(held => held.Subject == subject && held.Record.Standing && Same(held.Record.Purpose, purpose));

        if (standing < 0)
        {
            return ValueTask.FromResult(false);
        }

        _objections[standing] = (subject, _objections[standing].Record with { WithdrawnAt = at });

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<HeldConsent>> LiveAgainstAnotherAsync(
        IReadOnlyCollection<string> purposes,
        string document,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(purposes);

        return ValueTask.FromResult<IReadOnlyList<HeldConsent>>(
        [
            .. _consents
                .Where(held => held.Record.Live
                    && purposes.Contains(held.Record.Purpose)
                    && (!string.Equals(held.Record.Document, document, StringComparison.Ordinal)
                        || !string.Equals(held.Record.NoticeVersion, version, StringComparison.Ordinal)))
                .Select(held => new HeldConsent(held.Subject, held.Record))
                .OrderBy(one => one.Consent.GrantedAt),
        ]);
    }

    private static bool Same(string one, string other) => string.Equals(one, other, StringComparison.Ordinal);
}
