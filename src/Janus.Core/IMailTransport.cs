using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What carries a mail out of the deployment. The library depends on no particular
/// mail server: a deployment registers one of these and nothing else changes.
/// </summary>
/// <remarks>Implements LIB-EXT-001, INT-MAIL-008, INT-GEN-001.</remarks>
public interface IMailTransport
{
    /// <summary>
    /// Hands one mail to the transport.
    /// </summary>
    /// <param name="mail">The rendered mail.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>
    /// Whether the transport took it. A failure is an attempt to retry, never a
    /// reason to count the send against a restriction.
    /// </returns>
    ValueTask<Result> SendAsync(MailMessage mail, CancellationToken cancellationToken);
}
