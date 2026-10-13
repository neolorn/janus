using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What carries one admitted message to its destination: it resolves the words and
/// calls the transport.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001, AUTH-ABUSE-004 and CONV-CONTENT-001. The library has already
/// admitted the message under its restrictions and written it to its outbox; the handler
/// decides no restriction and counts nothing. The library names the message and supplies
/// its values; the words, the channel's own payload and the delivery are the handler's,
/// and a deployment replaces the one it is given by registering another. It is never
/// called while a transaction is open.
/// </remarks>
public interface INotificationHandler
{
    /// <summary>
    /// Carries one admitted message, or says why it was not taken.
    /// </summary>
    /// <param name="request">The admitted message.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>
    /// Nothing where a transport took the message, or the failure, which leaves the
    /// message to a later attempt.
    /// </returns>
    ValueTask<Result> SendAsync(
        SendRequest request,
        CancellationToken cancellationToken);
}
