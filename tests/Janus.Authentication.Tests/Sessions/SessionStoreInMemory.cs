using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Tests.Sessions;

/// <summary>
/// Sessions held in memory, keyed as the table is: the row by its identifier, and the
/// secret by its fingerprint.
/// </summary>
internal sealed class SessionStoreInMemory : ISessionStore
{
    private readonly Dictionary<SessionId, Session> _sessions = [];
    private readonly Dictionary<string, SessionId> _secrets = [];

    /// <summary>
    /// Every session the store holds, ended ones among them.
    /// </summary>
    public IReadOnlyCollection<Session> All => _sessions.Values;

    /// <summary>
    /// The session a secret belongs to, whatever its state.
    /// </summary>
    /// <param name="secret">The secret.</param>
    /// <returns>The session, or nothing where the secret answers to none.</returns>
    public Session? Behind(OpaqueToken secret) =>
        _secrets.TryGetValue(Key(secret.Fingerprint()), out SessionId id) ? _sessions[id] : null;

    /// <inheritdoc/>
    public ValueTask<Session?> FindAsync(SessionId id, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_sessions.GetValueOrDefault(id));

    /// <inheritdoc/>
    public ValueTask<Session?> FindByFingerprintAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        return ValueTask.FromResult(
            _secrets.TryGetValue(Key(fingerprint), out SessionId id)
                ? _sessions.GetValueOrDefault(id)
                : null);
    }

    /// <inheritdoc/>
    public ValueTask AddAsync(Session session, byte[] fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[session.Id] = session;
        _secrets[Key(fingerprint)] = session.Id;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        _sessions[session.Id] = session;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ReplaceSecretAsync(
        SessionId id,
        byte[] fingerprint,
        CancellationToken cancellationToken)
    {
        foreach (string held in _secrets.Where(pair => pair.Value == id).Select(pair => pair.Key))
        {
            _secrets.Remove(held);
        }

        _secrets[Key(fingerprint)] = id;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Session>> LiveOfAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Session>>(
        [
            .. _sessions.Values.Where(session =>
                session.Subject == subject
                && session.EndedAt is null
                && at < session.AbsoluteExpiry
                && at < session.IdleExpiry),
        ]);

    /// <inheritdoc/>
    public ValueTask EndSpineAsync(
        SessionId spine,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        foreach (Session session in _sessions.Values.Where(session => session.Spine == spine))
        {
            session.End(at);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask EndAccountAsync(
        SubjectId subject,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        foreach (Session session in _sessions.Values.Where(session => session.Subject == subject))
        {
            session.End(at);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask EndEveryAsync(DateTimeOffset at, CancellationToken cancellationToken)
    {
        foreach (Session session in _sessions.Values)
        {
            session.End(at);
        }

        return ValueTask.CompletedTask;
    }

    private static string Key(byte[] fingerprint) => Convert.ToHexString(fingerprint);
}
