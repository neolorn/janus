using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Records;

namespace Janus.Authentication.Tests.Records;

/// <summary>
/// The collection sessions, held in memory as the table holds them: each read is a
/// copy of what was last written, so a step that is rolled back leaves the stored
/// session as it stood.
/// </summary>
internal sealed class CollectionSessionStoreInMemory : ICollectionSessionStore
{
    private readonly Dictionary<CollectionSessionId, CollectionSession> _sessions = [];

    /// <summary>
    /// Every session the store holds, as last written.
    /// </summary>
    public IReadOnlyCollection<CollectionSession> All => [.. _sessions.Values.Select(Copied)];

    /// <inheritdoc/>
    public ValueTask<CollectionSession?> FindAsync(CollectionSessionId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_sessions.TryGetValue(id, out CollectionSession? held) ? Copied(held) : null);

    /// <inheritdoc/>
    public ValueTask<CollectionSession?> FindForUpdateAsync(CollectionSessionId id, CancellationToken cancellationToken) =>
        FindAsync(id, cancellationToken);

    /// <inheritdoc/>
    public ValueTask AddAsync(CollectionSession session, CancellationToken cancellationToken)
    {
        _sessions[session.Id] = Copied(session);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(CollectionSession session, CancellationToken cancellationToken)
    {
        _sessions[session.Id] = Copied(session);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(CollectionSessionId id, CancellationToken cancellationToken)
    {
        _ = _sessions.Remove(id);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<CollectionSessionId> gone = [.. _sessions
            .Where(session => session.Value.HasExpired(now))
            .Select(session => session.Key)];

        foreach (CollectionSessionId id in gone)
        {
            _ = _sessions.Remove(id);
        }

        return ValueTask.FromResult(gone.Count);
    }

    private static CollectionSession Copied(CollectionSession session)
    {
        var copy = CollectionSession.Existing(
            session.Id,
            session.AsksAge,
            session.Language,
            session.CreatedAt,
            session.ExpiresAt,
            session.Contacts);

        copy.Restore(session.AdultAffirmed, session.Group, session.DateOfBirth, session.AnsweredAgeAt, session.AgeRefused);

        return copy;
    }
}
