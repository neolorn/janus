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
/// waiting for a message: each channel has a transport, every message exists in every
/// configured language, every text message fits one message in each of them, every
/// restriction naming a host key has a supplier, and no declared endpoint is plaintext.
/// </summary>
/// <param name="configuration">Where the languages and the restrictions come from.</param>
/// <param name="templates">
/// The catalogue in force, the deployment's own or the one the library ships.
/// </param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <param name="places">
/// The widths a text message's places are measured at for this deployment, or nothing
/// where it declared no landing origins, without which no link can be measured.
/// </param>
/// <param name="mailTransport">
/// What the deployment's mail leaves through, or nothing where it registered none.
/// </param>
/// <param name="smsTransport">
/// What the deployment's text messages leave through, or nothing where it registered
/// none.
/// </param>
/// <remarks>
/// Implements AUTH-ABUSE-005, INT-SMS-003, INT-SMS-005a, INT-SMS-006, INT-MAIL-008,
/// INT-GEN-001, INF-TLS-004, LIB-EXT-001 and LIB-HOST-001. Every deployment sends mail
/// and text messages, alerts among them, and the library ships no transport of its own
/// yet, so a deployment that registered none for a channel is refused here rather than
/// at its first send (D-166, 270). A recipient is never resolved to a language the
/// catalogue cannot answer in, because startup refuses that deployment. Declaring no
/// catalogue is not itself a refusal: the library ships one, and what is checked is the
/// catalogue in force, whichever it is (LIB-EXT-001).
/// </remarks>
internal sealed class SendingValidation(
    IConfigurationStore configuration,
    IMessageTemplates templates,
    RestrictionKeySuppliers suppliers,
    MessagePlaceholders? places,
    IMailTransport? mailTransport,
    ISmsTransport? smsTransport)
{
    private const string MailTransport = "mailTransport";

    private const string SmsTransport = "smsTransport";

    private const string Landing = "landingOrigins.authentication";

    /// <summary>
    /// Runs every check, answering with the first that fails.
    /// </summary>
    /// <param name="cancellationToken">Abandons the checks.</param>
    /// <returns>Nothing, or the failure that stops startup.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        if (mailTransport is null)
        {
            return Result.Failure(Absent(MailTransport));
        }

        if (smsTransport is null)
        {
            return Result.Failure(Absent(SmsTransport));
        }

        // LIB-HOST-001: the origins are the declaration check's to read in full; this
        // check runs first and measures a link against them, so it names the same
        // omission the declaration check would.
        if (places is null)
        {
            return Result.Failure(Absent(Landing));
        }

        if (await InsecureAsync(configuration, cancellationToken).ConfigureAwait(false) is Error insecure)
        {
            return Result.Failure(insecure);
        }

        Error? failure = null;

        IReadOnlyList<string> languages = (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        IReadOnlyList<Restriction> declared = (await configuration
                .ReadAsync(Settings.Restrictions, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<Restriction>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (Unsupplied(declared) is Error unsupplied)
        {
            return Result.Failure(unsupplied);
        }

        return Catalogued(languages, places) is Error missing ? Result.Failure(missing) : Result.Success();
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
    /// protected key from the server (D-166, 319). The addresses are the mail and text
    /// endpoints, the mail server adapter's endpoint and the corpus a deployment hosts
    /// itself. A deployment that supplies a transport of its own leaves its endpoint
    /// empty and calls its provider wherever it decides; nothing of the host's is
    /// registered here.
    /// </remarks>
    internal static async ValueTask<Error?> InsecureAsync(
        IConfigurationStore configuration,
        CancellationToken cancellationToken)
    {
        foreach (TextSetting key in new[]
        {
            Settings.IntegrationMailEndpoint,
            Settings.IntegrationSmsEndpoint,
            Settings.IntegrationMailServerEndpoint,
            Settings.PasswordBlocklistSelfHostedAddress,
        })
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

            if (endpoint.Length is not 0 && !Secure(endpoint))
            {
                return Error.From(
                    ErrorCodes.EndpointInsecure,
                    "key",
                    JsonSerializer.SerializeToElement(key.Key.ToString()));
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

    private Error? Catalogued(IReadOnlyList<string> languages, MessagePlaceholders measured)
    {
        foreach (MessageKind message in MessageChannels.Messages)
        {
            foreach (SendKind kind in MessageChannels.Of(message))
            {
                foreach (string language in languages)
                {
                    MessageTemplate? template = templates
                        .Find(message, kind, language)
                        .Match(found => (MessageTemplate?)found, _ => null);

                    if (template is null)
                    {
                        return Absent(WrittenName.Of(message) + "." + WrittenName.Of(kind) + "." + language);
                    }

                    if (kind is not SendKind.Sms)
                    {
                        continue;
                    }

                    // One character past the budget costs another message, which for
                    // a non-Latin language is seventy characters in (AUTH-ABUSE-005).
                    // The template is measured with every place it names at its widest,
                    // because nothing is measured at the moment of a send, and one that
                    // carries a link is given the two segments a link needs.
                    string widest = measured.Widest(template.Text);
                    bool linked = MessagePlaceholders.CarriesLink(template.Text);

                    if (MessageBudget.Exceeds(widest, linked))
                    {
                        return new Error(
                            ErrorCodes.ConfigurationValueNotAllowed,
                            new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                            {
                                ["key"] = JsonSerializer.SerializeToElement(
                                    WrittenName.Of(message) + "." + WrittenName.Of(kind) + "." + language),
                                ["allowed"] = JsonSerializer.SerializeToElement(
                                    MessageBudget.Of(widest, linked)),
                            });
                    }
                }
            }
        }

        return null;
    }
}
