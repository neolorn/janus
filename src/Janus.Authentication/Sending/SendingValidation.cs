using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// What is checked once, at startup, so that nothing fails at the moment a person is
/// waiting for a message: the deployment registered what takes its messages and its
/// alerts, every restriction naming a host key has a supplier, and no declared endpoint
/// is plaintext.
/// </summary>
/// <param name="configuration">Where the restrictions and the endpoints come from.</param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <param name="notificationHandler">
/// What takes the deployment's messages, or nothing where it registered none.
/// </param>
/// <param name="alertHandler">
/// What takes the deployment's raised alerts, or nothing where it registered none.
/// </param>
/// <remarks>
/// Implements INT-SMS-006, OPS-ALERT-003, INT-GEN-001, LIB-EXT-001 and LIB-HOST-001
/// (D-225). The library sends no message and delivers no alert itself, so a deployment
/// that registered nothing to take either is refused here rather than at its first send.
/// </remarks>
internal sealed class SendingValidation(
    IConfigurationStore configuration,
    RestrictionKeySuppliers suppliers,
    INotificationHandler? notificationHandler,
    IAlertHandler? alertHandler)
{
    private const string NotificationHandler = "notificationHandler";

    private const string AlertHandler = "alertHandler";

    // INT-GEN-001: the addresses the library itself calls out to, in the order the
    // start names the first that is not over TLS.
    private static readonly IReadOnlyList<TextSetting> Endpoints =
    [
        Settings.IntegrationMailServerEndpoint,
        Settings.PasswordBlocklistSelfHostedAddress,
    ];

    /// <summary>
    /// Runs every check, answering with the first that fails.
    /// </summary>
    /// <param name="cancellationToken">Abandons the checks.</param>
    /// <returns>Nothing, or the failure that stops startup.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        // INT-SMS-006: every message leaves through the host's handler.
        if (notificationHandler is null)
        {
            return Result.Failure(Absent(NotificationHandler));
        }

        // OPS-ALERT-003: every raised alert is delivered by the host's handler.
        if (alertHandler is null)
        {
            return Result.Failure(Absent(AlertHandler));
        }

        if (await InsecureAsync(configuration, cancellationToken).ConfigureAwait(false) is Error insecure)
        {
            return Result.Failure(insecure);
        }

        Error? failure = null;

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        return Unsupplied(declared) is Error unsupplied ? Result.Failure(unsupplied) : Result.Success();
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Error Absent(string key) =>
        Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(key));

    /// <summary>
    /// The endpoint rule: every address the library itself calls out to is an absolute
    /// https address, or empty.
    /// </summary>
    /// <param name="configuration">Where the addresses are read.</param>
    /// <param name="cancellationToken">Abandons the reads.</param>
    /// <returns>Nothing, or <c>integration.endpoint.insecure</c> naming the key.</returns>
    /// <remarks>
    /// Implements INT-GEN-001 and INF-TLS-004 for the start and for a change of a
    /// protected key from the server (D-166, 319). The addresses are the mail server
    /// adapter's endpoint and the corpus a deployment hosts itself; the library sends no
    /// message, and a host's own integrations are the host's (D-162, D-225).
    /// </remarks>
    internal static async ValueTask<Error?> InsecureAsync(
        IConfigurationStore configuration,
        CancellationToken cancellationToken)
    {
        foreach (TextSetting key in Endpoints)
        {
            // The self-hosted corpus's address is a key the deployment names only where
            // it hosts the corpus, so one never named is no endpoint to call.
            string endpoint = (await configuration.ReadAsync(key, cancellationToken)
                .ConfigureAwait(false))
                .Match(
                    value => value,
                    error => error.Code == ErrorCodes.StartupDeclarationMissing
                        ? string.Empty
                        : throw new InvalidOperationException(error.Code.ToString()));

            if (Insecure(key.Key, endpoint) is Error insecure)
            {
                return insecure;
            }
        }

        return null;
    }

    /// <summary>
    /// The endpoint rule over one value: what a key naming an address the library calls
    /// out to holds, or is about to hold, is an absolute https address, or empty.
    /// </summary>
    /// <param name="key">The key the value is read from or written to.</param>
    /// <param name="endpoint">The value.</param>
    /// <returns>
    /// Nothing where the key names no such address or the value keeps the rule, or
    /// <c>integration.endpoint.insecure</c> naming the key.
    /// </returns>
    /// <remarks>
    /// Implements INT-GEN-001 and INF-TLS-004 for a change of a runtime setting as for
    /// the start (D-198), so one rule judges an address at both.
    /// </remarks>
    internal static Error? Insecure(ConfigurationKey key, string endpoint)
    {
        foreach (TextSetting named in Endpoints)
        {
            if (named.Key == key)
            {
                return endpoint.Length is 0 || Secure(endpoint)
                    ? null
                    : Error.From(
                        ErrorCodes.EndpointInsecure,
                        "key",
                        JsonSerializer.SerializeToElement(key.ToString()));
            }
        }

        return null;
    }

    // An address that is not an absolute https address is not one the library calls,
    // whether it names another scheme or is not an address at all.
    private static bool Secure(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? parsed)
        && string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    private Error? Unsupplied(IReadOnlyList<Restriction> declared)
    {
        foreach (Restriction restriction in declared)
        {
            if (restriction.Key is not RestrictionKeyKind.Host)
            {
                continue;
            }

            if (restriction.HostKeyName is null || !suppliers.TryFind(restriction.HostKeyName, out _))
            {
                return Error.From(
                    ErrorCodes.StartupDeclarationMissing,
                    "supplier",
                    JsonSerializer.SerializeToElement(restriction.HostKeyName ?? restriction.Name));
            }
        }

        return null;
    }
}
