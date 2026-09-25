using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Oidc;

/// <summary>
/// What the library will forward a browser to, read once at startup: the origins of
/// the registered clients' return destinations, and the client a destination falls
/// back to where the one a request named is not one of them.
/// </summary>
/// <param name="clients">The registry both the list and the default come from.</param>
/// <param name="configuration">Where the default client is named.</param>
/// <remarks>
/// Implements API-REDIR-001 and API-REDIR-002. The registry is the one list, so the
/// destinations and the origins cannot drift apart, and a destination that is not an
/// absolute origin is found here rather than at the moment a person is forwarded to
/// it. A protocol client is no place to land a browser, so the default names a
/// browser application or startup refuses it.
/// </remarks>
internal sealed class RedirectValidation(IOidcClientStore clients, IConfigurationStore configuration)
{
    /// <summary>
    /// Reads the registry and the configured default, answering with the first
    /// entry that will not resolve.
    /// </summary>
    /// <param name="cancellationToken">Abandons the checks.</param>
    /// <returns>Nothing, or the failure that stops startup.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<OidcClient> registered =
            await clients.AllAsync(cancellationToken).ConfigureAwait(false);

        foreach (OidcClient client in registered)
        {
            if (!Origin(client.Redirect))
            {
                return Result.Failure(Refused("client", client.ClientId));
            }
        }

        string named = (await configuration
                .ReadAsync(Settings.RedirectDefaultClient, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, _ => string.Empty);

        if (named.Length is 0)
        {
            return Result.Success();
        }

        return Lands(registered, named)
            ? Result.Success()
            : Result.Failure(Refused("key", Settings.RedirectDefaultClient.Key.ToString()));
    }

    private static Error Refused(string member, string value) =>
        Error.From(
            ErrorCodes.StartupRedirectClient,
            member,
            JsonSerializer.SerializeToElement(value));

    /// <summary>
    /// Whether a browser can be sent to the address: it is absolute and carries a host,
    /// which is what having an origin at all means (API-REDIR-001 AC3).
    /// </summary>
    /// <param name="destination">The address.</param>
    /// <returns>Whether it has an origin.</returns>
    public static bool Origin(string destination) =>
        Uri.TryCreate(destination, UriKind.Absolute, out Uri? parsed) && parsed.Host.Length > 0;

    private static bool Lands(IReadOnlyList<OidcClient> registered, string named)
    {
        foreach (OidcClient client in registered)
        {
            if (string.Equals(client.ClientId, named, StringComparison.Ordinal))
            {
                return client.Kind is OidcClientKind.BrowserApplication;
            }
        }

        return false;
    }
}
