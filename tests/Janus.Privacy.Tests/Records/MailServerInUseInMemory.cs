using Janus.Core;

namespace Janus.Privacy.Tests.Records;

/// <summary>
/// The mail server in use as a start that has already chosen leaves it: the one set,
/// or none.
/// </summary>
internal sealed class MailServerInUseInMemory : IMailServerInUse
{
    /// <summary>
    /// The mail server in use, or nothing where the deployment registered none.
    /// </summary>
    public IMailServer? Server { get; set; }

    /// <inheritdoc/>
    public Result<IMailServer> Chosen() =>
        Server is IMailServer server
            ? Result.Success(server)
            : Result.Failure<IMailServer>(Error.From(ErrorCodes.MailboxNotFound));
}
