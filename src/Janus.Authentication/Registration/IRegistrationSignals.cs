using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// What tells a waiting stream that a registration session has changed.
/// </summary>
/// <remarks>
/// Implements REG-SESS-003 and FE-VER-001. The browser that presses a verification
/// link is not promised to reach the instance the stream is open on, so the signal
/// crosses the deployment rather than the process. A wait that no signal reaches ends
/// on the interval instead, which is what the stream reads the state back on.
/// </remarks>
internal interface IRegistrationSignals
{
    /// <summary>
    /// Waits for the next change to a session, or for the interval to pass.
    /// </summary>
    /// <param name="session">The registration session.</param>
    /// <param name="interval">How long to wait where nothing signals.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of waiting.</returns>
    ValueTask WaitAsync(
        RegistrationSessionId session,
        TimeSpan interval,
        CancellationToken cancellationToken);
}
