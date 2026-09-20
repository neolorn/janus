using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// Where a bot-defence signal is written down when no challenge can be shown.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-008 and IDN-AUD-001.</remarks>
internal interface IBotDefenceAudit
{
    /// <summary>
    /// Records a signal that fired.
    /// </summary>
    /// <param name="signal">Which signal.</param>
    /// <param name="source">The address it fired for.</param>
    /// <param name="challenged">Whether a challenge was shown.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask SignalledAsync(
        BotDefenceSignal signal,
        string source,
        bool challenged,
        DateTimeOffset at,
        CancellationToken cancellationToken);
}
