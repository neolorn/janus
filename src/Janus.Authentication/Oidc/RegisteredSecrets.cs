using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// The secrets of the registered clients, and the rotation that keeps them fresh.
/// </summary>
/// <param name="clients">The registry the secrets are held in.</param>
/// <param name="configuration">Where the cadence and the access-token lifetime come from.</param>
/// <param name="work">The one transaction a rotation runs in.</param>
/// <param name="randomness">What a new secret is drawn from.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements OPS-SEC-002, OPS-SEC-001 and BFF-SESS-006, as D-166 (340) settles them.
/// Both ends of every registered flow are the library's, so no person chooses, carries
/// or rotates a secret: the library draws it, holds it wrapped, and the first read of a
/// secret that has reached the cadence of the signing keys replaces it. The replaced
/// one is taken for the overlap the signing keys keep, the access-token lifetime and
/// five minutes. Of two processes rotating together one replaces the secret and the
/// other reads what it wrote.
/// </remarks>
internal sealed class RegisteredSecrets(
    IOidcClientStore clients,
    IConfigurationStore configuration,
    IUnitOfWork work,
    RandomNumberGenerator randomness,
    TimeProvider time)
{
    // How many bytes a secret is drawn from, as a token of the library is (OpaqueToken).
    private const int Drawn = 32;

    // AUTH-KEY-001: the margin over the access-token lifetime the signing keys keep.
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Draws a secret: thirty-two bytes, written base64url.
    /// </summary>
    /// <param name="randomness">What the bytes are drawn from.</param>
    /// <returns>The secret as a client presents it, as its UTF-8 bytes, which the caller clears.</returns>
    /// <exception cref="ArgumentNullException">The source is absent.</exception>
    public static byte[] Draw(RandomNumberGenerator randomness)
    {
        ArgumentNullException.ThrowIfNull(randomness);

        byte[] drawn = new byte[Drawn];

        try
        {
            randomness.GetBytes(drawn);

            return Base64Url.EncodeToUtf8(drawn);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(drawn);
        }
    }

    /// <summary>
    /// The secret a registered client presents now, replacing it first where it has
    /// reached the cadence.
    /// </summary>
    /// <param name="clientId">Which client.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The secret as its UTF-8 bytes, which the caller clears, or the refusal where a
    /// setting could not be read or no such client is registered.
    /// </returns>
    public async ValueTask<Result<byte[]>> CurrentAsync(string clientId, CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan cadence = (await configuration
                .ReadAsync(Settings.TokenSigningRotation, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        TimeSpan lifetime = (await configuration
                .ReadAsync(Settings.OidcAccessTokenLifetime, cancellationToken).ConfigureAwait(false))
            .Match(read => read, error => Withheld<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<byte[]>(failure);
        }

        RegisteredSecret? held = await clients.SecretAsync(clientId, cancellationToken).ConfigureAwait(false);

        if (held is null)
        {
            return Result.Failure<byte[]>(Error.From(ErrorCodes.SystemFault));
        }

        try
        {
            DateTimeOffset now = time.GetUtcNow();

            return now < held.IssuedAt + cadence
                ? Result.Success(held.Current.AsSpan().ToArray())
                : await RotatedAsync(clientId, held.IssuedAt, now, now + lifetime + Margin, cancellationToken)
                    .ConfigureAwait(false);
        }
        finally
        {
            Clear(held);
        }
    }

    private static void Clear(RegisteredSecret secret)
    {
        CryptographicOperations.ZeroMemory(secret.Current);

        if (secret.Previous is byte[] previous)
        {
            CryptographicOperations.ZeroMemory(previous);
        }
    }

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // OPS-SEC-002: the new secret becomes current and the one it replaces is taken for
    // the overlap, in one transaction conditional on the issue read (X3); a process
    // that lost the race reads the secret the winner wrote.
    private async ValueTask<Result<byte[]>> RotatedAsync(
        string clientId,
        DateTimeOffset issuedAt,
        DateTimeOffset now,
        DateTimeOffset replacedUntil,
        CancellationToken cancellationToken)
    {
        byte[] fresh = Draw(randomness);

        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<byte[]>(notBegun);
        }

        bool replaced = await clients
            .ReplaceSecretAsync(clientId, issuedAt, fresh, now, replacedUntil, cancellationToken)
            .ConfigureAwait(false);

        if (replaced)
        {
            return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
                .Match(() => Result.Success(fresh), Result.Failure<byte[]>);
        }

        // CONV-DESIGN-003: where another process replaced it first, nothing was written
        // here, so the unit of work is rolled back before the standing secret is read.
        await work.RollbackAsync().ConfigureAwait(false);

        CryptographicOperations.ZeroMemory(fresh);

        RegisteredSecret? standing = await clients.SecretAsync(clientId, cancellationToken).ConfigureAwait(false);

        if (standing is null)
        {
            return Result.Failure<byte[]>(Error.From(ErrorCodes.SystemFault));
        }

        try
        {
            return Result.Success(standing.Current.AsSpan().ToArray());
        }
        finally
        {
            Clear(standing);
        }
    }
}
