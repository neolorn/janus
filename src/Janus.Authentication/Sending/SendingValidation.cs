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
/// waiting for a message: every message exists in every configured language, every
/// text message fits one message in each of them, every restriction naming a host
/// key has a supplier, and no declared endpoint is plaintext.
/// </summary>
/// <param name="configuration">Where the languages and the restrictions come from.</param>
/// <param name="templates">
/// The catalogue in force, the deployment's own or the one the library ships.
/// </param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <remarks>
/// Implements AUTH-ABUSE-005, INT-SMS-003, INT-SMS-005a, INT-GEN-001, INF-TLS-004 and
/// LIB-HOST-001. A recipient is never resolved to a language the catalogue cannot
/// answer in, because startup refuses that deployment. Declaring no catalogue is not
/// itself a refusal: the library ships one, and what is checked is the catalogue in
/// force, whichever it is (LIB-EXT-001).
/// </remarks>
internal sealed class SendingValidation(
    IConfigurationStore configuration,
    IMessageTemplates templates,
    RestrictionKeySuppliers suppliers)
{
    /// <summary>
    /// Runs every check, answering with the first that fails.
    /// </summary>
    /// <param name="cancellationToken">Abandons the checks.</param>
    /// <returns>Nothing, or the failure that stops startup.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        if (await InsecureAsync(cancellationToken).ConfigureAwait(false) is Error insecure)
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

        return Catalogued(languages) is Error missing ? Result.Failure(missing) : Result.Success();
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

    // INT-GEN-001, INF-TLS-004: the addresses the library itself calls out to, the
    // mail and text endpoints and the corpus a deployment hosts itself. A deployment
    // that supplies a transport of its own leaves its endpoint empty and calls its
    // provider wherever it decides; nothing of the host's is registered here.
    private async ValueTask<Error?> InsecureAsync(CancellationToken cancellationToken)
    {
        foreach (TextSetting key in new[]
        {
            Settings.IntegrationMailEndpoint,
            Settings.IntegrationSmsEndpoint,
            Settings.PasswordBlocklistSelfHostedAddress,
        })
        {
            string endpoint = (await configuration.ReadAsync(key, cancellationToken)
                .ConfigureAwait(false))
                .Match(value => value, _ => string.Empty);

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

    private Error? Catalogued(IReadOnlyList<string> languages)
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

                    // One character past the budget costs a second message, which for
                    // a non-Latin language is seventy characters in (AUTH-ABUSE-005).
                    // The template is measured with every place it names at its widest,
                    // because nothing is measured at the moment of a send.
                    string widest = MessagePlaceholders.Widest(template.Text);

                    if (MessageBudget.Exceeds(widest))
                    {
                        return new Error(
                            ErrorCodes.ConfigurationValueNotAllowed,
                            new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                            {
                                ["key"] = JsonSerializer.SerializeToElement(
                                    WrittenName.Of(message) + "." + WrittenName.Of(kind) + "." + language),
                                ["allowed"] = JsonSerializer.SerializeToElement(
                                    MessageBudget.Of(widest)),
                            });
                    }
                }
            }
        }

        return null;
    }
}
