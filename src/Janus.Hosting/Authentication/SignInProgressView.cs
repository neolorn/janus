using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// Where a sign-in stands, as the authentication application receives it.
/// </summary>
/// <param name="Status">Where it stands.</param>
/// <param name="AssuranceLevel">What it has reached.</param>
/// <param name="PhishingResistant">Whether what was presented resists phishing.</param>
/// <param name="Required">What is still wanted.</param>
/// <param name="TrustDeviceOffered">Whether the policy permits trusting this browser.</param>
/// <param name="PolicyRequirement">
/// The raised requirement and the instant its run-up ends, where one stands.
/// </param>
/// <param name="PasswordChangeRequired">
/// Whether the password has to be changed before anything else.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-016, AUTH-FACT-017 and AUTH-SESS-002. The
/// properties reached are reported and never which entry produced them; the session
/// crosses as a cookie and never as a field.
/// </remarks>
internal sealed record SignInProgressView(
    SignInStatus Status,
    AssuranceLevel AssuranceLevel,
    bool PhishingResistant,
    IReadOnlyList<Factor> Required,
    bool TrustDeviceOffered,
    PolicyRequirement? PolicyRequirement,
    bool PasswordChangeRequired)
{
    /// <summary>
    /// Reads what a step reached.
    /// </summary>
    /// <param name="progress">What the step reached.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The progress is absent.</exception>
    public static SignInProgressView Of(SignInProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new SignInProgressView(
            progress.Status,
            progress.AssuranceLevel,
            progress.PhishingResistant,
            progress.Required,
            progress.TrustDeviceOffered,
            progress.Requirement,
            progress.PasswordChangeRequired);
    }
}
