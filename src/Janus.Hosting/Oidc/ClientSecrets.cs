using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Storage.Authentication.Oidc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;

namespace Janus.Hosting.Oidc;

/// <summary>
/// How a client's secret is judged: against what the registry holds it hashes to, in
/// constant time.
/// </summary>
/// <param name="cache">The server's own cache of registry entries.</param>
/// <param name="logger">Where the server records what it did with an entry.</param>
/// <param name="options">The server's own options.</param>
/// <param name="store">The registry.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 AC2 and CONV-SEC-002. The registry holds the fingerprint
/// of the secret and never the secret, and the comparison takes the same time whether
/// the first byte differs or the last, so nothing about a wrong secret is learned from
/// how long the answer took.
/// </remarks>
internal sealed class ClientSecrets(
    IOpenIddictApplicationCache<OidcClientRecord> cache,
    ILogger<OpenIddictApplicationManager<OidcClientRecord>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<OidcClientRecord> store)
    : OpenIddictApplicationManager<OidcClientRecord>(cache, logger, options, store)
{
    /// <inheritdoc/>
    protected override ValueTask<bool> ValidateClientSecretAsync(
        string secret,
        string comparand,
        CancellationToken cancellationToken)
    {
        if (secret is not { Length: > 0 } || comparand is not { Length: > 0 })
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(
            Base64.IsValid(comparand)
            && CryptographicOperations.FixedTimeEquals(
                OpaqueToken.Of(secret).Fingerprint(),
                Convert.FromBase64String(comparand)));
    }

    /// <inheritdoc/>
    protected override ValueTask<string> ObfuscateClientSecretAsync(
        string secret,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Convert.ToBase64String(OpaqueToken.Of(secret).Fingerprint()));
}
