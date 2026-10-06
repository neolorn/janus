using Janus.Core;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// The mail server in use as a start that has already chosen leaves it: the one given,
/// or none.
/// </summary>
/// <param name="server">The mail server in use, or nothing where there is none.</param>
internal sealed class MailServerInUseInMemory(IMailServer? server) : IMailServerInUse
{
    /// <inheritdoc/>
    public Result<IMailServer> Chosen() =>
        server is null
            ? Result.Failure<IMailServer>(Error.From(ErrorCodes.MailboxNotFound))
            : Result.Success(server);
}
