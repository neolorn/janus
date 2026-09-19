using System;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Authentication.Sending;

/// <summary>
/// What counts registration sessions per source, which is the second bot-defence
/// signal.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-008 and CONV-DESIGN-003.</remarks>
internal interface IRegistrationSources
{
    /// <summary>
    /// Counts one registration session against the source that started it.
    /// </summary>
    /// <param name="source">The address.</param>
    /// <param name="at">When it started.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of counting it.</returns>
    ValueTask RecordAsync(string source, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>
    /// How many registration sessions one source started since an instant.
    /// </summary>
    /// <param name="source">The address.</param>
    /// <param name="from">The earliest counted.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The count.</returns>
    ValueTask<int> SinceAsync(string source, DateTimeOffset from, CancellationToken cancellationToken);
}
