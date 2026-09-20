using Janus.Core;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The catalogue a deployment declares, answering every message on every channel in
/// every language, which is what a deployment that has written its messages looks
/// like to the check that runs before anything is served.
/// </summary>
internal sealed class MessageTemplatesInMemory : IMessageTemplates
{
    /// <inheritdoc/>
    public Result<MessageTemplate> Find(MessageKind message, SendKind kind, string language) =>
        Result.Success(new MessageTemplate(
            kind is SendKind.Email ? "your code" : null,
            "your code is 429184"));
}
