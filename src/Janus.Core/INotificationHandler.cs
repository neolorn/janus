using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What the host registers to take every message the library owes: it renders it and
/// sends it through the host's own messaging.
/// </summary>
/// <remarks>
/// Implements INT-SMS-006, LIB-EXT-001, AUTH-ABUSE-004 and CONV-CONTENT-001 (D-225). Every
/// deployment registers one, and startup refuses one that has none. The library has
/// already admitted the message under its restrictions and written it to its outbox;
/// the handler decides no restriction and counts nothing. It is never called while a
/// transaction is open.
/// </remarks>
public interface INotificationHandler
{
    /// <summary>
    /// Takes one admitted message, or says why it was not taken.
    /// </summary>
    /// <param name="request">The admitted message.</param>
    /// <param name="cancellationToken">Abandons the hand-over.</param>
    /// <returns>
    /// Nothing where the host has recorded the message to be sent, or the failure, which
    /// leaves the message to a later attempt under the same reference.
    /// </returns>
    ValueTask<Result> SendAsync(
        SendRequest request,
        CancellationToken cancellationToken);
}
