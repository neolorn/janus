using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Tests;

/// <summary>
/// The consents a registration records, held per subject and purpose so a test can
/// see what the terms step wrote and what it left alone.
/// </summary>
/// <remarks>
/// Only the purposes named here are taken: a grant for anything else is refused, as
/// the privacy area refuses a purpose the deployment does not take consent for.
/// </remarks>
internal sealed class ConsentsInMemory : IConsents
{
    private readonly Dictionary<(SubjectId Subject, string Purpose), ConsentRecord> _held = [];

    private readonly HashSet<string> _taken = new(StringComparer.Ordinal);

    /// <summary>
    /// How many records exist, over every subject.
    /// </summary>
    public int Recorded => _held.Count;

    /// <summary>
    /// What the deployment takes consent for.
    /// </summary>
    /// <param name="purpose">The purpose.</param>
    public void Take(string purpose) => _taken.Add(purpose);

    /// <summary>
    /// What one subject holds.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <returns>The records, in purpose order.</returns>
    public IReadOnlyList<ConsentRecord> Of(SubjectId subject) =>
    [
        .. _held
            .Where(one => one.Key.Subject == subject)
            .OrderBy(one => one.Key.Purpose, StringComparer.Ordinal)
            .Select(one => one.Value),
    ];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ConsentRecord>>> ReadAsync(
        AccessContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.FromResult(
            context.Effective is SubjectId subject
                ? Result.Success(Of(subject))
                : Result.Failure<IReadOnlyList<ConsentRecord>>(Error.From(ErrorCodes.Denied)));
    }

    /// <inheritdoc/>
    public ValueTask<Result> GrantAsync(
        AccessContext context,
        string purpose,
        ConsentMechanism mechanism,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is not SubjectId subject || !_taken.Contains(purpose))
        {
            return ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.Denied)));
        }

        _held[(subject, purpose)] = new ConsentRecord(
            purpose,
            "1",
            mechanism,
            ConsentKind.Ordinary,
            DateTimeOffset.UnixEpoch,
            WithdrawnAt: null,
            SupersededAt: null);

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result> WithdrawAsync(
        AccessContext context,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Effective is SubjectId subject
            && _held.TryGetValue((subject, purpose), out ConsentRecord? held))
        {
            _held[(subject, purpose)] = held with { WithdrawnAt = DateTimeOffset.UnixEpoch };
        }

        return ValueTask.FromResult(Result.Success());
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ObjectionRecord>>> ObjectionsAsync(
        AccessContext context,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success<IReadOnlyList<ObjectionRecord>>([]));

    /// <inheritdoc/>
    public ValueTask<Result> ObjectAsync(
        AccessContext context,
        string purpose,
        ConsentMechanism mechanism,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.PurposeNotObjectable)));

    /// <inheritdoc/>
    public ValueTask<Result> WithdrawObjectionAsync(
        AccessContext context,
        string purpose,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Failure(Error.From(ErrorCodes.Denied)));
}
