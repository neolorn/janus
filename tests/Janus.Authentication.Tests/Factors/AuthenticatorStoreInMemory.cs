using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// Enrolled credentials held in memory, keyed as the table is.
/// </summary>
internal sealed class AuthenticatorStoreInMemory : IAuthenticatorStore
{
    private readonly Dictionary<AuthenticatorId, Authenticator> _held = [];

    /// <summary>
    /// Every credential the store holds.
    /// </summary>
    public IReadOnlyCollection<Authenticator> All => _held.Values;

    /// <summary>
    /// Places a credential in the store as a completed enrolment would.
    /// </summary>
    /// <param name="authenticator">The credential.</param>
    public void Hold(Authenticator authenticator)
    {
        ArgumentNullException.ThrowIfNull(authenticator);

        _held[authenticator.Id] = authenticator;
    }

    /// <inheritdoc/>
    public ValueTask<Authenticator?> FindAsync(
        AuthenticatorId id,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.GetValueOrDefault(id));

    /// <inheritdoc/>
    public ValueTask<Authenticator?> ByCredentialAsync(
        ReadOnlyMemory<byte> credentialId,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_held.Values.FirstOrDefault(credential =>
            credential.WebAuthn is not null
            && credential.WebAuthn.CredentialId.Span.SequenceEqual(credentialId.Span)));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<Authenticator>> OfAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<Authenticator>>(
            [.. _held.Values.Where(credential => credential.Subject == subject)]);

    /// <inheritdoc/>
    public ValueTask AddAsync(Authenticator authenticator, CancellationToken cancellationToken)
    {
        Hold(authenticator);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RecordAsync(Authenticator authenticator, CancellationToken cancellationToken)
    {
        Hold(authenticator);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(AuthenticatorId id, CancellationToken cancellationToken)
    {
        _held.Remove(id);

        return ValueTask.CompletedTask;
    }
}
