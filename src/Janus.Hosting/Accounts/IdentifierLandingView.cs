using System;
using Janus.Core;

namespace Janus.Hosting.Accounts;

/// <summary>
/// What a verification link answers when it changed nothing.
/// </summary>
/// <param name="SameBrowser">Whether the link was opened where it was sent from.</param>
/// <param name="Code">
/// The code to type where the flow is waiting, present only where the link was opened
/// somewhere else.
/// </param>
/// <remarks>Implements REG-SESS-003 and API-LAND-001.</remarks>
internal sealed record IdentifierLandingView(bool SameBrowser, string? Code)
{
    /// <summary>
    /// Reads a landing.
    /// </summary>
    /// <param name="landing">What the landing resolved to.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The landing is absent.</exception>
    public static IdentifierLandingView Of(LinkLanding landing)
    {
        ArgumentNullException.ThrowIfNull(landing);

        return new IdentifierLandingView(landing.SameBrowser, landing.Code);
    }
}
