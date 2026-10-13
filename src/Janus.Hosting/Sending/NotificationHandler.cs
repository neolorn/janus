using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Hosting.Sending;

/// <summary>
/// The shipped handler: it words one admitted message from the catalogue of the
/// deployment and hands it to the transport of its channel.
/// </summary>
/// <param name="configuration">Where the declared languages come from.</param>
/// <param name="templates">Where the words come from.</param>
/// <param name="mail">What carries a mail.</param>
/// <param name="sms">What carries a text message.</param>
/// <remarks>
/// Implements LIB-EXT-001, INT-SMS-001, INT-GEN-005, IDN-ATTR-001, AUTH-ABUSE-004 and
/// CONV-CONTENT-001. It decides no restriction and counts nothing: the message it is
/// given was admitted and written to the outbox before it was asked. A mail owed in
/// every declared language is one message, composed here from each language's text in
/// the order the deployment declares them; no catalogue holds a text in more than one
/// language.
/// </remarks>
internal sealed class NotificationHandler(
    IConfigurationStore configuration,
    IMessageTemplates templates,
    IMailTransport mail,
    ISmsTransport sms) : INotificationHandler
{
    // What stands between the subject lines of a mail in every declared language.
    private const string BetweenSubjects = " | ";

    // What stands between the texts of a mail in every declared language.
    private const string BetweenTexts = "\n\n";

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    /// <exception cref="ArgumentException">A text message names no language.</exception>
    public async ValueTask<Result> SendAsync(SendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind is SendKind.Sms)
        {
            // A text message is one message per language (AUTH-ABUSE-004), so the
            // library admits each under a language of its own.
            string language = request.Language
                ?? throw new ArgumentException("A text message names its language.", nameof(request));

            return await Worded(request, language).Match(
                    template => sms.SendAsync(
                        new SmsMessage(
                            request.Destination.Phone,
                            MessageRendering.Fill(template.Text, request.Values),
                            request.Reference.Value),
                        cancellationToken),
                    error => ValueTask.FromResult(Result.Failure(error)))
                .ConfigureAwait(false);
        }

        Error? failure = null;

        IReadOnlyList<string> languages = request.Language is string named
            ? [named]
            : (await configuration
                    .ReadAsync(Settings.NotificationLanguages, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Held<IReadOnlyList<string>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        if (languages.Count == 0)
        {
            return Result.Failure(Undeclared());
        }

        var worded = new List<MessageTemplate>(languages.Count);

        foreach (string language in languages)
        {
            MessageTemplate? template = Worded(request, language)
                .Match(found => (MessageTemplate?)found, error => Held<MessageTemplate?>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure(failure);
            }

            worded.Add(template!);
        }

        // INT-GEN-005: the payload of an outbound message is built here and nowhere else,
        // so its field set is one thing to read and one thing to test.
        return await mail
            .SendAsync(
                new MailMessage(
                    request.Destination.Mail,
                    string.Join(
                        BetweenSubjects,
                        worded.Select(one => MessageRendering.Fill(one.Subject ?? string.Empty, request.Values))),
                    string.Join(
                        BetweenTexts,
                        worded.Select(one => MessageRendering.Fill(one.Text, request.Values))),
                    request.Reference.Value),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    private static Error Undeclared() =>
        Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(Settings.NotificationLanguages.Key.ToString()));

    // Which key the catalogue could not answer for is the library's to name; the
    // failure the catalogue itself produced says nothing the operator can act on.
    private Result<MessageTemplate> Worded(SendRequest request, string language) =>
        templates
            .Find(request.Message, request.Kind, language)
            .Match(Result.Success, _ => Result.Failure<MessageTemplate>(Undeclared()));
}
