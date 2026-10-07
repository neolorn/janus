using System.Text.Json;
using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// One factor presented, against a sign-in or against a live session.
/// </summary>
/// <param name="ChallengeId">The handle the sign-in began with.</param>
/// <param name="Factor">Which entry is presented.</param>
/// <param name="Value">
/// What is presented: what was typed, as text, or what the authenticator answered a
/// ceremony with, the <c>AuthenticationResponseJSON</c> as a JSON object
/// (AUTH-FACT-014).
/// </param>
/// <param name="LinkToken">The token of the link, where one was opened.</param>
/// <param name="TrustDevice">
/// Whether this browser is to be trusted, offered only on the call that completes a
/// two-factor sign-in.
/// </param>
/// <param name="Press">
/// Whether the person pressed the control the link landed on. A link completes on a
/// press and a plain open completes nothing.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-014, AUTH-FACT-015, AUTH-STEP-001, REG-SESS-003
/// and API-LAND-001.
/// </remarks>
internal sealed record PresentFactorRequest(
    string? ChallengeId,
    Factor Factor,
    [property: NeverLogged] JsonElement? Value,
    [property: NeverLogged] string? LinkToken,
    bool TrustDevice,
    bool Press)
{
    /// <summary>
    /// Whether the request presents anything: text that was typed, or an object in the
    /// place of an authenticator's answer.
    /// </summary>
    /// <returns>Whether it does.</returns>
    public bool Presents() =>
        Value is { ValueKind: JsonValueKind.Object }
        || (Value is { ValueKind: JsonValueKind.String } typed && typed.GetString() is { Length: > 0 });

    /// <summary>
    /// Reads the factor as it is presented.
    /// </summary>
    /// <returns>
    /// The presentation, or nothing where <c>value</c> is neither text nor an
    /// authenticator's answer that reads.
    /// </returns>
    public FactorPresentation? Presented()
    {
        switch (Value)
        {
            case null or { ValueKind: JsonValueKind.Null }:
                return new FactorPresentation(Factor) { TrustDevice = TrustDevice };

            case { ValueKind: JsonValueKind.String } typed:
                return new FactorPresentation(Factor) { Value = typed.GetString(), TrustDevice = TrustDevice };

            case { ValueKind: JsonValueKind.Object } answered:
                return AuthenticationResponseJson.Read(answered) is AuthenticatorAssertion assertion
                    ? new FactorPresentation(Factor) { Assertion = assertion, TrustDevice = TrustDevice }
                    : null;

            default:
                return null;
        }
    }
}
