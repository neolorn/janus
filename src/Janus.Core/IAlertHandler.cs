using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What takes each raised alert from the library and delivers it as the host chooses:
/// to whom, by which channel, the owner's included.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-003 and LIB-EXT-001. The library decides that an alert is
/// raised and deduplicates it; it delivers none itself. Every deployment registers one,
/// or startup refuses it. It is never called while a transaction is open.
/// </remarks>
public interface IAlertHandler
{
    /// <summary>
    /// Takes one raised alert, or says why it was not taken.
    /// </summary>
    /// <param name="alert">The alert, as the row that raised it carries it.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>
    /// Nothing where the host took the alert, or the failure, which leaves the alert to
    /// be handed over again at the next pass.
    /// </returns>
    ValueTask<Result> TakeAsync(AlertRaised alert, CancellationToken cancellationToken);
}
