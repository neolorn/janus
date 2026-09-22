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
/// The catalogue of the deployment, absent where it has declared none.
/// </param>
/// <param name="suppliers">The host-registered key suppliers.</param>
/// <param name="endpoints">The declared outbound addresses.</param>
/// <remarks>
/// Implements AUTH-ABUSE-005, INT-SMS-003, INT-SMS-005a, INT-GEN-001 and
/// LIB-HOST-001. A recipient is never resolved to a language the catalogue cannot
/// answer in, because startup refuses that deployment; a deployment that declared no
/// catalogue at all answers in none of them and is refused the same way.
/// </remarks>
internal sealed class SendingValidation(
    IConfigurationStore configuration,
    IMessageTemplates? templates,
    RestrictionKeySuppliers suppliers,
    IntegrationEndpoints endpoints)
{
    /// <summary>
    /// Runs every check, answering with the first that fails.
    /// </summary>
    /// <param name="cancellationToken">Abandons the checks.</param>
    /// <returns>Nothing, or the failure that stops startup.</returns>
    public async ValueTask<Result> ValidateAsync(CancellationToken cancellationToken)
    {
        if (Insecure() is Error insecure)
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

    private Error? Insecure()
    {
        foreach (IntegrationEndpoint endpoint in endpoints.Insecure)
        {
            return new Error(
                ErrorCodes.EndpointInsecure,
                new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
                {
                    ["integration"] = JsonSerializer.SerializeToElement(endpoint.Integration),
                    ["key"] = JsonSerializer.SerializeToElement(endpoint.Key),
                });
        }

        return null;
    }

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
                    MessageTemplate? template = templates?
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
