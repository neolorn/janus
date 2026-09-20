using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Registration;
using Janus.Core;

namespace Janus.Authentication.Tests.Registration;

/// <summary>
/// The registration sessions, held in memory and found the two ways the table is
/// read: by identifier, and by the link a message carried.
/// </summary>
internal sealed class RegistrationSessionStoreInMemory : IRegistrationSessionStore
{
    private readonly Dictionary<RegistrationSessionId, RegistrationSession> _sessions = [];

    /// <summary>
    /// Every session the store holds.
    /// </summary>
    public IReadOnlyCollection<RegistrationSession> All => _sessions.Values;

    /// <inheritdoc/>
    public ValueTask<RegistrationSession?> FindAsync(
        RegistrationSessionId id,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_sessions.GetValueOrDefault(id));

    /// <inheritdoc/>
    public ValueTask<RegistrationSession?> FindByLinkAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_sessions.Values.FirstOrDefault(session => Sent(session, fingerprint)));

    /// <inheritdoc/>
    public ValueTask AddAsync(RegistrationSession session, CancellationToken cancellationToken)
    {
        _sessions[session.Id] = session;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(RegistrationSession session, CancellationToken cancellationToken)
    {
        _sessions[session.Id] = session;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(RegistrationSessionId id, CancellationToken cancellationToken)
    {
        _ = _sessions.Remove(id);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<RegistrationSessionId> gone = [.. _sessions
            .Where(session => session.Value.HasExpired(now))
            .Select(session => session.Key)];

        foreach (RegistrationSessionId id in gone)
        {
            _ = _sessions.Remove(id);
        }

        return ValueTask.FromResult(gone.Count);
    }

    private static bool Sent(RegistrationSession session, byte[] fingerprint) =>
        session.Identifiers.Any(staged =>
            staged.Link is byte[] link && link.SequenceEqual(fingerprint));
}
