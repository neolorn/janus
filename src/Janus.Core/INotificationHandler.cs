using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What carries one of the library's messages to one destination: which message, to
/// whom, in which language.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001, AUTH-ABUSE-004 and CONV-CONTENT-001. The library names the
/// message and supplies its values; the words, the channel's own payload and the
/// delivery are the handler's, and a deployment replaces the one it is given by
/// registering another. A refusal by a named restriction carries <c>retryAt</c>, so a
/// caller learns when the send would be allowed without learning anything about the
/// destination.
/// </remarks>
public interface INotificationHandler
{
    /// <summary>
    /// Sends one message, or says why it was not sent.
    /// </summary>
    /// <param name="request">What is to be sent.</param>
    /// <param name="cancellationToken">Abandons the send.</param>
    /// <returns>
    /// The correlation reference it was taken under, or the failure. A message that
    /// goes out in every declared language is one message per language, each under a
    /// reference of its own, and the reference returned is the first language's.
    /// </returns>
    ValueTask<Result<SendReference>> SendAsync(
        SendRequest request,
        CancellationToken cancellationToken);
}
