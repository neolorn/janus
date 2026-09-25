using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication;
using Janus.Core;
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
/// <param name="time">The clock a replaced secret's overlap is read against.</param>
/// <remarks>
/// Implements AUTH-OIDC-001 AC2, OPS-SEC-002 and CONV-SEC-002. The registry holds the
/// fingerprint of the secret and never the secret, and the comparison takes the same
/// time whether the first byte differs or the last, so nothing about a wrong secret is
/// learned from how long the answer took. A secret the current one replaced is taken
/// until its overlap ends, and both are compared whichever one matches.
/// </remarks>
internal sealed class ClientSecrets(
    IOpenIddictApplicationCache<OidcClientRecord> cache,
    ILogger<OpenIddictApplicationManager<OidcClientRecord>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<OidcClientRecord> store,
    TimeProvider time)
    : OpenIddictApplicationManager<OidcClientRecord>(cache, logger, options, store)
{
    /// <inheritdoc/>
    public override ValueTask<bool> ValidateClientSecretAsync(
        OidcClientRecord application,
        [NeverLogged] string secret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (string.IsNullOrWhiteSpace(secret))
        {
            return ValueTask.FromResult(false);
        }

        byte[] presented = OpaqueToken.Of(secret).Fingerprint();

        bool current = Matches(presented, application.Secret);
        bool replaced = application.PreviousSecretUntil is DateTimeOffset until
            && time.GetUtcNow() < until
            && Matches(presented, application.PreviousSecret);

        return ValueTask.FromResult(current | replaced);
    }

    private static bool Matches(byte[] presented, byte[]? held) =>
        held is { Length: > 0 } && CryptographicOperations.FixedTimeEquals(presented, held);

    /// <inheritdoc/>
    protected override ValueTask<string> ObfuscateClientSecretAsync(
        [NeverLogged] string secret,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Convert.ToBase64String(OpaqueToken.Of(secret).Fingerprint()));
}
