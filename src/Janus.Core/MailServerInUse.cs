using System;
using System.Threading;

namespace Janus.Core;

/// <summary>
/// The mail server in use, chosen once by the service that fills the key ring and held
/// for the life of the process.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-007 and D-176. It is registered where the key ring is, and only
/// the application's start chooses: no command asks it, since a push a command owes is
/// written to the outbox the application's worker delivers.
/// </remarks>
internal sealed class MailServerInUse : IMailServerInUse
{
    private const int Unchosen = 0;

    private const int Chose = 1;

    private IMailServer? _server;

    private int _state;

    /// <inheritdoc/>
    public Result<IMailServer> Chosen()
    {
        if (Volatile.Read(ref _state) is Unchosen)
        {
            throw new InvalidOperationException("The mail server in use is read before the start chose it.");
        }

        return _server is IMailServer server
            ? Result.Success(server)
            : Result.Failure<IMailServer>(Error.From(ErrorCodes.MailboxNotFound));
    }

    /// <summary>
    /// Records the choice the start made.
    /// </summary>
    /// <param name="server">The mail server in use, or nothing where there is none.</param>
    /// <exception cref="InvalidOperationException">A choice was already made.</exception>
    internal void Choose(IMailServer? server)
    {
        _server = server;

        if (Interlocked.CompareExchange(ref _state, Chose, Unchosen) is not Unchosen)
        {
            throw new InvalidOperationException("The mail server in use is chosen once.");
        }
    }
}
