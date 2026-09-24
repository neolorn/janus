using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Credentials;
using Janus.Core;
using Janus.Hosting.Callbacks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Janus.Hosting.Credentials;

/// <summary>
/// The keys each declared social provider publishes, read from its own documents and
/// held between events, and what verifies an event against them.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012a and INT-GEN-003. The issuer and the key set are read from
/// the address the deployment declared and from nothing an event carries; a key the
/// provider has rotated in is asked for again when an event names it, and the event
/// that named it is refused meanwhile, for the provider to deliver again. An event's
/// lifetime is judged where it states one; a security event usually states none, and
/// is not refused for that.
/// </remarks>
internal sealed class ProviderKeys
{
    /// <summary>
    /// The client the providers' documents are read with, which a host configures the
    /// way it configures every other client of the framework's factory.
    /// </summary>
    public const string Channel = "identity-providers";

    private readonly FrozenDictionary<Factor, Declared> _declared;

    private readonly TimeProvider _time;

    private readonly ILogger<ProviderKeys> _log;

    /// <summary>
    /// Holds the providers the deployment declared.
    /// </summary>
    /// <param name="providers">What the deployment declared.</param>
    /// <param name="channel">Where the providers' documents are read from.</param>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <param name="log">Where a provider whose documents could not be read is recorded.</param>
    public ProviderKeys(
        IEnumerable<SocialProvider> providers,
        IHttpClientFactory channel,
        TimeProvider time,
        ILogger<ProviderKeys> log)
    {
        _time = time;
        _log = log;

        // LIB-HOST-001: a provider declared twice stops the deployment at startup, so
        // the first is the one there is.
        _declared = providers
            .DistinctBy(provider => provider.Provider)
            .ToFrozenDictionary(
                provider => provider.Provider,
                provider => new Declared(
                    provider,
                    new ConfigurationManager<ProviderMetadata>(
                        provider.Metadata.AbsoluteUri,
                        new ProviderMetadataReading(),
                        new ProviderDocuments(channel))));
    }

    /// <summary>
    /// Whether the deployment declared a provider.
    /// </summary>
    /// <param name="provider">Which social provider.</param>
    /// <returns>Whether it did.</returns>
    public bool Declares(Factor provider) => _declared.ContainsKey(provider);

    /// <summary>
    /// Verifies an event against the keys its provider publishes: signed by one of
    /// them, issued by the provider, and addressed to one of the deployment's clients.
    /// </summary>
    /// <param name="provider">Which social provider it claims to come from.</param>
    /// <param name="token">The event as it arrived.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it verifies.</returns>
    /// <exception cref="ArgumentNullException">The event is absent.</exception>
    public async ValueTask<bool> VerifiesAsync(
        Factor provider,
        [NeverLogged] string token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (!_declared.TryGetValue(provider, out Declared? declared))
        {
            return false;
        }

        if ((await MetadataAsync(declared, cancellationToken).ConfigureAwait(false))
            .Match<ProviderMetadata?>(read => read, _ => null) is not ProviderMetadata metadata)
        {
            return false;
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = metadata.Issuer,
            ValidAudiences = declared.Provider.ClientIds,
            IssuerSigningKeys = metadata.Keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            LifetimeValidator = (before, expires, _, _) =>
                (before is null || before <= _time.GetUtcNow().UtcDateTime)
                && (expires is null || expires > _time.GetUtcNow().UtcDateTime),
            TryAllIssuerSigningKeys = false,
        };

        TokenValidationResult read = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token, parameters)
            .ConfigureAwait(false);

        if (read.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            declared.Metadata.RequestRefresh();
        }

        return read.IsValid;
    }

    // The provider's issuer and keys, as held or read again; documents that could not
    // be read verify nothing.
    private async ValueTask<Result<ProviderMetadata>> MetadataAsync(
        Declared declared,
        CancellationToken cancellationToken)
    {
        try
        {
            return Result.Success(
                await declared.Metadata.GetConfigurationAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (InvalidOperationException)
        {
            // IDX20803: the documents could not be read.
            CallbackLog.Unreadable(_log, ProviderEvents.CallbackOf(declared.Provider.Provider));

            return Result.Failure<ProviderMetadata>(Error.From(ErrorCodes.CallbackRejected));
        }
    }

    private sealed record Declared(SocialProvider Provider, ConfigurationManager<ProviderMetadata> Metadata);
}
