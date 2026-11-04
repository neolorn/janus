using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// Where each caller's staff searches are counted, one row a minute, across the
/// deployment.
/// </summary>
/// <remarks>Implements IDN-SRCH-002 and CONV-DESIGN-003 (D-256).</remarks>
internal interface ISearchWindows
{
    /// <summary>
    /// Counts one search in its minute and forgets the caller's earlier minutes, in one
    /// statement.
    /// </summary>
    /// <param name="subject">The searching account.</param>
    /// <param name="window">The minute, truncated.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>How many searches the caller has made in that minute, this one included.</returns>
    ValueTask<int> CountAsync(SubjectId subject, DateTimeOffset window, CancellationToken cancellationToken);
}
