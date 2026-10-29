using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Authentication.Tests.Records;

/// <summary>
/// The record access sessions, held in memory as the table holds them: each read is a
/// copy of what was last written.
/// </summary>
internal sealed class RecordAccessSessionStoreInMemory : IRecordAccessSessionStore
{
    private readonly Dictionary<RecordAccessSessionId, RecordAccessSession> _sessions = [];

    /// <summary>
    /// Every session the store holds, as last written.
    /// </summary>
    public IReadOnlyCollection<RecordAccessSession> All => [.. _sessions.Values.Select(Copied)];

    /// <inheritdoc/>
    public ValueTask<RecordAccessSession?> FindAsync(RecordAccessSessionId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_sessions.TryGetValue(id, out RecordAccessSession? held) ? Copied(held) : null);

    /// <inheritdoc/>
    public ValueTask<RecordAccessSession?> FindForUpdateAsync(RecordAccessSessionId id, CancellationToken cancellationToken) =>
        FindAsync(id, cancellationToken);

    /// <inheritdoc/>
    public ValueTask AddAsync(RecordAccessSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[session.Id] = Copied(session);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(RecordAccessSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[session.Id] = Copied(session);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(RecordAccessSessionId id, CancellationToken cancellationToken)
    {
        _ = _sessions.Remove(id);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> EndReachingAsync(SubjectId record, CancellationToken cancellationToken)
    {
        List<RecordAccessSessionId> gone = [.. _sessions
            .Where(session => session.Value.Reached.Contains(record))
            .Select(session => session.Key)];

        foreach (RecordAccessSessionId id in gone)
        {
            _ = _sessions.Remove(id);
        }

        return ValueTask.FromResult(gone.Count);
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<RecordAccessSessionId> gone = [.. _sessions
            .Where(session => session.Value.HasExpired(now))
            .Select(session => session.Key)];

        foreach (RecordAccessSessionId id in gone)
        {
            _ = _sessions.Remove(id);
        }

        return ValueTask.FromResult(gone.Count);
    }

    private static RecordAccessSession Copied(RecordAccessSession session) =>
        RecordAccessSession.Existing(
            session.Id,
            session.Kind,
            [.. session.Fingerprints],
            session.Throttled,
            session.CreatedAt,
            session.OpenedAt,
            session.ExpiresAt,
            [.. session.Reached]);
}
