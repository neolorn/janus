using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Requests;

/// <summary>
/// Where a message to a subject goes. The area names the message and the subject;
/// which channels carry it, in what language and with what words is the one sending
/// path's business and the deployment catalogue's.
/// </summary>
/// <remarks>
/// Implements CONV-CONTENT-001, CONV-DESIGN-003 and LIB-EXT-001.
/// </remarks>
internal interface ISubjectNotices
{
    /// <summary>
    /// Tells one subject one thing.
    /// </summary>
    /// <param name="subject">Who.</param>
    /// <param name="message">Which message.</param>
    /// <param name="source">What is sending it, for the send ledger.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many channels took it.</returns>
    ValueTask<int> TellAsync(
        SubjectId subject,
        MessageKind message,
        string source,
        CancellationToken cancellationToken);
}
