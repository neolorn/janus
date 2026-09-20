using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// What a sign-in begins with, as the authentication application receives it.
/// </summary>
/// <param name="ChallengeId">The handle every later step presents.</param>
/// <param name="Available">The policy's enabled primary entries.</param>
/// <param name="WebAuthn">The assertion challenge, always present.</param>
/// <remarks>
/// Implements AUTH-ABUSE-003 and AUTH-FACT-002. The answer is identical for an
/// identifier that resolves to an account and one that resolves to nothing.
/// </remarks>
internal sealed record SignInChallengeView(
    string ChallengeId,
    IReadOnlyList<Factor> Available,
    WebAuthnChallenge WebAuthn)
{
    /// <summary>
    /// Reads a challenge.
    /// </summary>
    /// <param name="challenge">What the sign-in opened with.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The challenge is absent.</exception>
    public static SignInChallengeView Of(SignInChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        return new SignInChallengeView(
            challenge.Challenge,
            challenge.Available,
            challenge.WebAuthn);
    }
}
