using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Exports;

/// <summary>
/// Where the parts of an export that other areas hold are read: the account and how
/// it was registered, the profile, the identifiers, the preferences, and the live
/// sessions with their location records.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003 and CONV-DESIGN-003. One port rather than one per area,
/// because an export is one read of what is held at one instant: parts assembled from
/// several reads could describe an account that never stood that way.
/// </remarks>
internal interface IExportSource
{
    /// <summary>
    /// Everything the identity and authentication tables hold about one subject.
    /// </summary>
    /// <param name="subject">Whose.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The sections, in reading order.</returns>
    ValueTask<IReadOnlyList<ExportSection>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken);
}
