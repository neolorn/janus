using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Oidc;

/// <summary>
/// What the server signs with: the deployment's own key, imported once when it changes
/// rather than once for every token.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The key is the one the store holds, so a
/// rotation takes effect in every process that reaches it and needs no restart; the
/// one it replaced is kept until the next rotation, because a signature begun with it
/// has to finish. No key of the server's own is ever registered: a token signed with
/// one would validate against a set the deployment does not publish.
/// </remarks>
internal sealed class SigningCredentialSource : IDisposable
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    private SigningCredentials? _current;

    private ECDsa? _signing;

    private ECDsa? _previous;

    /// <summary>
    /// What the server signs with now, once something has read it.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing has read it yet.</exception>
    public SigningCredentials Current =>
        _current ?? throw new InvalidOperationException(
            "The deployment's signing key has not been read yet.");

    /// <summary>
    /// What the server signs with, reading the store for the key signing now and
    /// importing it where it is not the one already held.
    /// </summary>
    /// <param name="keys">Where the key signing now is read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The credentials, or the refusal the store answered with.</returns>
    /// <exception cref="ArgumentNullException">The key store is absent.</exception>
    public async ValueTask<Result<SigningCredentials>> CurrentAsync(
        SigningKeys keys,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);

        Error? failure = null;

        SigningMaterial material = (await keys.SigningAsync(cancellationToken).ConfigureAwait(false))
            .Match(signing => signing, error => Withheld(error, ref failure));

        if (failure is Error refusal)
        {
            return Result.Failure<SigningCredentials>(refusal);
        }

        try
        {
            if (_current is SigningCredentials held
                && string.Equals(held.Key.KeyId, material.KeyId, StringComparison.Ordinal))
            {
                return Result.Success(held);
            }

            return Result.Success(await ImportedAsync(material, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(material.PrivateKey);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _signing?.Dispose();
        _previous?.Dispose();
        _gate.Dispose();
    }

    private static SigningMaterial Withheld(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private async ValueTask<SigningCredentials> ImportedAsync(
        SigningMaterial material,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_current is SigningCredentials held
                && string.Equals(held.Key.KeyId, material.KeyId, StringComparison.Ordinal))
            {
                return held;
            }

            var imported = ECDsa.Create();
            imported.ImportPkcs8PrivateKey(material.PrivateKey, out _);

            // The key before last can no longer be signing with, because the one after
            // it has been the current key since the rotation before this one.
            _previous?.Dispose();
            _previous = _signing;
            _signing = imported;

            _current = new SigningCredentials(
                new ECDsaSecurityKey(imported) { KeyId = material.KeyId },
                material.Algorithm is "ES256"
                    ? SecurityAlgorithms.EcdsaSha256
                    : throw new InvalidOperationException(
                        "The signing algorithm names no signature this version writes."));

            return _current;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}
