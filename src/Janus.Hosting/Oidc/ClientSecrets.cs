using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Oidc;
using Janus.Core;
using Janus.Storage.Authentication.Oidc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace Janus.Hosting.Oidc;

/// <summary>
/// How a client's secret is judged: against the secrets the registry holds for it, in
/// constant time.
/// </summary>
/// <param name="cache">The server's own cache of registry entries.</param>
/// <param name="logger">Where the server records what it did with an entry.</param>
/// <param name="options">The server's own options.</param>
/// <param name="store">The registry, as the server reads it.</param>
/// <param name="secrets">
/// The read of the secrets the registry holds, which removes a replaced one whose
/// overlap has ended.
/// </param>
/// <param name="time">The clock a replaced secret's overlap is read against.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 AC2, OPS-SEC-002 and CONV-SEC-002, as D-166 (340) settles
/// them. The registry holds the secrets wrapped; they are unwrapped for the comparison
/// and cleared after it, and the comparison takes the same time whether the first byte
/// differs or the last, so nothing about a wrong secret is learned from how long the
/// answer took. A secret the current one replaced is taken until its overlap ends, and
/// both are compared whichever one matches; the read removes it once that overlap has
/// ended (OPS-SEC-002 AC3). A secret is drawn and replaced by the client
/// registry alone, so the server never has a secret of its own to hold.
/// </remarks>
internal sealed class ClientSecrets(
    IOpenIddictApplicationCache<OidcClientRecord> cache,
    ILogger<OpenIddictApplicationManager<OidcClientRecord>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<OidcClientRecord> store,
    RegisteredSecrets secrets,
    TimeProvider time)
    : OpenIddictApplicationManager<OidcClientRecord>(cache, logger, options, store)
{
    /// <inheritdoc/>
    public override async ValueTask<bool> ValidateClientSecretAsync(
        OidcClientRecord application,
        [NeverLogged] string secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        // OPS-SEC-002 AC3: the read removes a replaced secret whose overlap has ended. A
        // read that could not is a secret not judged, which is refused.
        RegisteredSecret? held = (await secrets
                .HeldAsync(application.ClientId, cancellationToken)
                .ConfigureAwait(false))
            .Match(read => read, _ => null);

        if (held is null)
        {
            return false;
        }

        byte[] presented = Encoding.UTF8.GetBytes(secret);

        try
        {
            bool current = CryptographicOperations.FixedTimeEquals(presented, held.Current);
            bool replaced = held.PreviousUntil is DateTimeOffset until
                && time.GetUtcNow() < until
                && held.Previous is byte[] previous
                && CryptographicOperations.FixedTimeEquals(presented, previous);

            return current | replaced;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presented);
            CryptographicOperations.ZeroMemory(held.Current);

            if (held.Previous is byte[] previous)
            {
                CryptographicOperations.ZeroMemory(previous);
            }
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Always: no secret reaches the registry through the server.</exception>
    protected override ValueTask<string> ObfuscateClientSecretAsync(
        [NeverLogged] string secret,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A client's secret is drawn and held by the client registry alone.");
}
