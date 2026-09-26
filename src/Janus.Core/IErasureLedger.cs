using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// Where each erasure is written down outside the database, so that a restore to a
/// point before it cannot quietly take it back. The library writes the lines; a
/// deployment registers one of these over storage that does not share fate with the
/// database host, and the restore procedure replays what it holds with the
/// <c>replay-erasures</c> command.
/// </summary>
/// <remarks>
/// Implements DR-016, DR-006a and LIB-EXT-001. Each line is the erasure's instant in
/// RFC 3339 UTC to the second, one space, the subject identifier, one space, the
/// reason in the spelling of chapter 10 section 5.12a; nothing else about the person is
/// in it. An erasure's delivery is not complete until its line is appended, and one
/// whose line cannot be appended is retried and raised as every required subscriber's
/// is. A deployment that registers none completes its erasures without a ledger, which
/// is the residual R-A13 accepts until the tier upgrade.
/// </remarks>
public interface IErasureLedger
{
    /// <summary>
    /// Appends one line, followed by a line feed, to the end of the ledger. The same
    /// line may arrive again after a failure elsewhere; a replay reads a repeat as the
    /// one erasure it is.
    /// </summary>
    /// <param name="line">The line, without its terminator.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing once the line is durable, or the failure where it could not be made so.
    /// </returns>
    ValueTask<Result> AppendAsync(string line, CancellationToken cancellationToken);
}
