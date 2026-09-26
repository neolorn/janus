using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// Where the records each person was given are counted by day, and where each person's
/// daily mean is kept.
/// </summary>
/// <remarks>
/// Implements OPS-ALERT-005. A day's count is added to in one statement, so two queries
/// reported together are both counted.
/// </remarks>
internal interface IReadVolumeStore
{
    /// <summary>
    /// Adds records to one person's count for one day.
    /// </summary>
    /// <param name="actor">Who was given them.</param>
    /// <param name="day">The calendar day they were given on.</param>
    /// <param name="records">How many.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The day's count once they are added.</returns>
    ValueTask<long> AddAsync(SubjectId actor, DateOnly day, int records, CancellationToken cancellationToken);

    /// <summary>
    /// One person's daily mean as it was last computed.
    /// </summary>
    /// <param name="actor">Whose.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The mean, or zero where the person read nothing inside the window.</returns>
    ValueTask<decimal> BaselineAsync(SubjectId actor, CancellationToken cancellationToken);

    /// <summary>
    /// Computes every person's daily mean over the days of the window before today, and
    /// forgets every count older than the window.
    /// </summary>
    /// <param name="today">The calendar day now.</param>
    /// <param name="days">How many days the window holds.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>How many people have a mean.</returns>
    ValueTask<int> RebaselineAsync(DateOnly today, int days, CancellationToken cancellationToken);
}
