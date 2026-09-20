using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// What is written down when the restrictions themselves change. The plain key value
/// is never among it: a grant records the restriction, the credit and the reason
/// (AUTH-ABUSE-004).
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004, IDN-AUD-001 and CONV-DESIGN-003.</remarks>
internal interface ISendAudit
{
    /// <summary>
    /// Records an edit to one restriction.
    /// </summary>
    /// <param name="restriction">Which restriction.</param>
    /// <param name="before">What it was, and absent where the edit adds it.</param>
    /// <param name="after">What it became, and absent where the edit deletes it.</param>
    /// <param name="loosening">Whether the edit lets more through than before.</param>
    /// <param name="reason">The written reason, which a loosening requires.</param>
    /// <param name="actor">Who made it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask EditedAsync(
        string restriction,
        Restriction? before,
        Restriction? after,
        bool loosening,
        string? reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records credit added to one key under one restriction.
    /// </summary>
    /// <param name="restriction">Which restriction.</param>
    /// <param name="credit">How many sends the credit is worth.</param>
    /// <param name="reason">The written reason, which a grant requires.</param>
    /// <param name="actor">Who granted it.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask GrantedAsync(
        string restriction,
        int credit,
        string reason,
        SubjectId actor,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
