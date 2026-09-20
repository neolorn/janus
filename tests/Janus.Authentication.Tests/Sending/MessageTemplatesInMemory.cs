using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The deployment's catalogue, answering with whatever a test put in it and, unless
/// the test says otherwise, with a short template for every message and channel.
/// </summary>
internal sealed class MessageTemplatesInMemory : IMessageTemplates
{
    private readonly Dictionary<string, MessageTemplate> _held = [];

    /// <summary>
    /// Whether a message the catalogue was not given is answered with a short
    /// default rather than a failure.
    /// </summary>
    public bool Complete { get; set; } = true;

    /// <summary>
    /// Every message, channel and language the catalogue was asked for, in order,
    /// so a test can read what a check covered.
    /// </summary>
    public List<(MessageKind Message, SendKind Kind, string Language)> Asked { get; } = [];

    /// <summary>
    /// Puts one template in the catalogue.
    /// </summary>
    /// <param name="message">What the message is for.</param>
    /// <param name="kind">The channel.</param>
    /// <param name="language">The language.</param>
    /// <param name="template">The text.</param>
    public void Set(MessageKind message, SendKind kind, string language, MessageTemplate template) =>
        _held[Named(message, kind, language)] = template;

    /// <inheritdoc/>
    public Result<MessageTemplate> Find(MessageKind message, SendKind kind, string language)
    {
        Asked.Add((message, kind, language));

        if (_held.TryGetValue(Named(message, kind, language), out MessageTemplate? template))
        {
            return Result.Success(template);
        }

        return Complete
            ? Result.Success(new MessageTemplate(kind is SendKind.Email ? "subject" : null, "text"))
            : Result.Failure<MessageTemplate>(Error.From(ErrorCodes.StartupDeclarationMissing));
    }

    private static string Named(MessageKind message, SendKind kind, string language) =>
        message + "." + kind + "." + language;
}
