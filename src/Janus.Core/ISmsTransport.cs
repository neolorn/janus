using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What carries a text message out of the deployment, and what says how much credit
/// is left with the gateway carrying it.
/// </summary>
/// <remarks>
/// Implements LIB-EXT-001, INT-SMS-006, INT-SMS-004, INT-GEN-001. No provider name
/// appears in the library; a deployment registers one of these.
/// </remarks>
public interface ISmsTransport
{
    /// <summary>
    /// Hands one text message to the transport.
    /// </summary>
    /// <param name="message">The rendered message.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>
    /// Whether the transport took it. A failure is an attempt to retry, never a
    /// reason to count the send against a restriction.
    /// </returns>
    ValueTask<Result> SendAsync(SmsMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// What the gateway says is left on the account, in the currency it reports.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The balance, or the failure where the gateway did not answer.</returns>
    ValueTask<Result<decimal>> BalanceAsync(CancellationToken cancellationToken);
}
