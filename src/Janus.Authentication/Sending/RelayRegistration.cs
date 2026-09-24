using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// Whether mail to an Apple private relay address can arrive: wherever Continue with
/// Apple is a way in, the sending domain has to be one the deployment declared as
/// registered with the relay.
/// </summary>
/// <param name="configuration">Where the policy, the sending domain and the declaration are read.</param>
/// <param name="events">Where the warning goes.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <remarks>
/// Implements INT-MAIL-011 and entry 269. A relay address counts as verified by the
/// sign-in and can become an account's primary email, and the relay drops mail from an
/// unregistered domain without a word, so the deployment is told rather than the
/// person, who cannot be.
/// </remarks>
internal sealed class RelayRegistration(
    IConfigurationStore configuration,
    IEvents events,
    TimeProvider time)
{
    /// <summary>
    /// Raises <c>relay-domain-unregistered</c> where Continue with Apple is a way in and
    /// the sending domain is not declared as registered with the relay.
    /// </summary>
    /// <param name="cancellationToken">Abandons the check.</param>
    /// <returns>
    /// Nothing, or the failure where a setting could not be read or the warning was not
    /// taken.
    /// </returns>
    public async ValueTask<Result> CheckAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        Policy system = (await configuration
                .ReadAsync(Settings.PolicyDefault, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<Policy>(error, ref failure));

        string domain = (await configuration
                .ReadAsync(Settings.NotificationEmailSendingDomain, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<string>(error, ref failure));

        IReadOnlySet<string> registered = (await configuration
                .ReadAsync(Settings.NotificationEmailRelayRegistered, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlySet<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        // An organization's override can only narrow the system's login factors
        // (AUTH-STEP-002a), so an entry in any effective policy is an entry in this
        // one; the entry is known by what it may do (AUTH-FACT-001). A domain name is
        // the same name in any case (RFC 4343).
        if (!system.LoginFactors.Any(factor => FactorCatalogue.Of(factor).RelaysAddress)
            || registered.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Success();
        }

        return await events
            .PublishAsync(
                Alerts.Of(AlertCondition.RelayDomainUnregistered, domain, time.GetUtcNow(), Details(domain)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Dictionary<string, JsonElement> Details(string domain) =>
        new(capacity: 1, StringComparer.Ordinal)
        {
            ["domain"] = JsonSerializer.SerializeToElement(domain),
        };
}
