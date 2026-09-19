using Janus.Core.Configuration;

namespace Janus.Authentication.Passwords;

/// <summary>
/// Where screening records that it ran on something other than the corpus the
/// deployment configured.
/// </summary>
/// <remarks>
/// Implements INT-PWD-002 and AUTH-PASS-004: a screening failure is loud. The call is
/// a port because the structured log call itself is the host layer's, which is the
/// one place the logging abstraction exists.
/// </remarks>
internal interface IScreeningLog
{
    /// <summary>
    /// Records that the configured corpus could not answer and another did.
    /// </summary>
    /// <param name="configured">The corpus the deployment configured.</param>
    /// <param name="used">The corpus that answered.</param>
    void Degraded(BlocklistSource configured, BlocklistSource used);
}
