using System;
using Janus.Authentication.SignIn;

namespace Janus.Hosting.Authentication;

/// <summary>
/// What a sign-in link answers where it signed nobody in.
/// </summary>
/// <param name="SameBrowser">Whether it was opened where it was asked for.</param>
/// <param name="Code">
/// The code to type where the sign-in began, present only where the link was opened
/// somewhere else.
/// </param>
/// <remarks>Implements AUTH-FACT-003, REG-SESS-003 and API-LAND-001.</remarks>
internal sealed record SignInLandingView(bool SameBrowser, string? Code)
{
    /// <summary>
    /// Reads a landing.
    /// </summary>
    /// <param name="landing">What the landing resolved to.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The landing is absent.</exception>
    public static SignInLandingView Of(LandedSignIn landing)
    {
        ArgumentNullException.ThrowIfNull(landing);

        return new SignInLandingView(landing.SameBrowser, landing.Code);
    }
}
