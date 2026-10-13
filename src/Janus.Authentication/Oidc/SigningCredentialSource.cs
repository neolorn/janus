using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The signing keys the provider signs with, publishes and validates against, held as
/// one set for the life of the process and replaced whole at each change.
/// </summary>
/// <param name="scopes">Where the scope each change and each read of the stored keys runs in comes from.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements AUTH-KEY-001, AUTH-KEY-002 and CONV-CODE-007. Every token signed, every
/// request for the key set and every validation is a read, and the first read that finds
/// a change due by the times the set carries makes it: in a scope and a unit of work of
/// its own, never joined to one its caller holds open, and the set is replaced only once
/// that transaction has committed, or with what another process committed where it was
/// first. The first read, made at the application's start once the key ring is filled,
/// makes a key current where the database holds none, and the provider's options are
/// built after it with the current key's credential, the same object this holds.
/// </remarks>
internal sealed class SigningCredentialSource(IServiceScopeFactory scopes, TimeProvider time) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    private SigningKeySet? _set;

    /// <summary>
    /// The credential the provider's options are built with, which is the current key's
    /// as the start read it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The start has not read the keys yet.</exception>
    public SigningCredentials Started =>
        (Volatile.Read(ref _set)
            ?? throw new InvalidOperationException("The signing keys have not been read at the start yet."))
        .Signing;

    /// <summary>
    /// The set as it stands for this read, any change due made first.
    /// </summary>
    /// <param name="configuration">Where the cadence is read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The set, or the refusal where a setting could not be read or a change not committed.</returns>
    /// <exception cref="ArgumentNullException">The configuration store is absent.</exception>
    public async ValueTask<Result<SigningKeySet>> ReadAsync(
        IConfigurationStore configuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Error? failure = null;

        TimeSpan cadence = (await configuration
                .ReadAsync(Settings.TokenSigningRotation, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SigningKeySet>(failure);
        }

        SigningKeySet? set = Volatile.Read(ref _set);

        return set is not null && !set.IsChangeDue(time.GetUtcNow(), cadence)
            ? Result.Success(set)
            : await ChangedAsync(cadence, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// What a token is signed with: the current key, which, before it signs an access
    /// token under a longer lifetime than it carries, has that lifetime stored with it.
    /// </summary>
    /// <param name="configuration">Where the cadence and the access-token lifetime are read.</param>
    /// <param name="accessToken">Whether the token is an access token.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credential, or the refusal where a setting could not be read or a write not committed.</returns>
    /// <exception cref="ArgumentNullException">The configuration store is absent.</exception>
    public async ValueTask<Result<SigningCredentials>> SigningAsync(
        IConfigurationStore configuration,
        bool accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Error? failure = null;

        SigningKeySet set = (await ReadAsync(configuration, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<SigningKeySet>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<SigningCredentials>(failure);
        }

        if (!accessToken)
        {
            return Result.Success(set.Signing);
        }

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcAccessTokenLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        // AUTH-KEY-001 AC2, AC7: the longer lifetime is committed before the token is
        // signed; where the key was replaced meanwhile, the stored keys become the set and
        // the key now current carries the lifetime in turn.
        while (failure is null && set.Current.Key.LongestLifetime < lifetime)
        {
            SigningKey shorter = set.Current.Key;

            set = (await LengthenedAsync(shorter, lifetime, cancellationToken).ConfigureAwait(false))
                .Match(read => read, error => Withheld<SigningKeySet>(error, ref failure));

            if (failure is null
                && string.Equals(set.Current.Key.KeyId, shorter.KeyId, StringComparison.Ordinal)
                && set.Current.Key.LongestLifetime < lifetime)
            {
                throw new InvalidOperationException("The current signing key did not take the longer lifetime.");
            }
        }

        return failure is null ? Result.Success(set.Signing) : Result.Failure<SigningCredentials>(failure);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Volatile.Read(ref _set)?.Clear();
        _gate.Dispose();
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // One change at a time in this process; a read that waited behind another finds the
    // change made and reads the set it left.
    private async ValueTask<Result<SigningKeySet>> ChangedAsync(TimeSpan cadence, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_set is SigningKeySet set && !set.IsChangeDue(time.GetUtcNow(), cadence))
            {
                return Result.Success(set);
            }

            AsyncServiceScope scope = scopes.CreateAsyncScope();

            await using (scope.ConfigureAwait(false))
            {
                if ((await scope.ServiceProvider.GetRequiredService<SigningKeys>()
                        .ChangeAsync(cancellationToken)
                        .ConfigureAwait(false))
                    .Match<Error?>(() => null, error => error) is Error unchanged)
                {
                    return Result.Failure<SigningKeySet>(unchanged);
                }
            }

            return Result.Success(await ReplacedAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask<Result<SigningKeySet>> LengthenedAsync(
        SigningKey current,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            AsyncServiceScope scope = scopes.CreateAsyncScope();

            await using (scope.ConfigureAwait(false))
            {
                if ((await scope.ServiceProvider.GetRequiredService<SigningKeys>()
                        .LengthenAsync(current, lifetime, cancellationToken)
                        .ConfigureAwait(false))
                    .Match<Error?>(() => null, error => error) is Error unwritten)
                {
                    return Result.Failure<SigningKeySet>(unwritten);
                }
            }

            return Result.Success(await ReplacedAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    // The stored keys, read after the change's transaction has ended, become the set;
    // the objects of the set it replaces that it does not carry are disposed.
    private async ValueTask<SigningKeySet> ReplacedAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            SigningKeys keys = scope.ServiceProvider.GetRequiredService<SigningKeys>();
            SigningKeySet? replaced = _set;
            SigningKeySet replacing = await SigningKeySet
                .ReplacingAsync(
                    replaced,
                    await keys.HeldAsync(cancellationToken).ConfigureAwait(false),
                    keys,
                    cancellationToken)
                .ConfigureAwait(false);

            Volatile.Write(ref _set, replacing);
            replaced?.Release(replacing);

            return replacing;
        }
    }
}
