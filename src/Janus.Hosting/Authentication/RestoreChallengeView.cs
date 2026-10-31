using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Janus.Authentication.SignIn;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// What the restore in place of a lapsed session begins with, as the frontend receives
/// it.
/// </summary>
/// <param name="ChallengeId">The handle the restore presents.</param>
/// <param name="Factors">Each of the passkey and the password that may restore it.</param>
/// <param name="WebAuthn">The request options naming the account's passkeys.</param>
/// <remarks>
/// Implements AUTH-SESS-005 and D-224 item 13 (`09` <c>POST /auth/restore/begin</c>).
/// </remarks>
internal sealed record RestoreChallengeView(
    string ChallengeId,
    IReadOnlyList<Factor> Factors,
    [property: JsonPropertyName("webauthn")]
    WebAuthnRequestOptions WebAuthn)
{
    /// <summary>
    /// Reads a restore's challenge.
    /// </summary>
    /// <param name="challenge">What the restore opened with.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The challenge is absent.</exception>
    public static RestoreChallengeView Of(RestoreChallenge challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        return new RestoreChallengeView(challenge.Challenge, challenge.Factors, challenge.WebAuthn);
    }
}
