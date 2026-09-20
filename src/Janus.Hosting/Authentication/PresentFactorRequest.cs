using Janus.Core;

namespace Janus.Hosting.Authentication;

/// <summary>
/// One factor presented, against a sign-in or against a live session.
/// </summary>
/// <param name="ChallengeId">The handle the sign-in began with.</param>
/// <param name="Factor">Which entry is presented.</param>
/// <param name="Value">What was typed.</param>
/// <param name="LinkToken">The token of the link, where one was opened.</param>
/// <param name="Assertion">What the authenticator answered, for a ceremony.</param>
/// <param name="TrustDevice">
/// Whether this browser is to be trusted, offered only on the call that completes a
/// two-factor sign-in.
/// </param>
/// <param name="Press">
/// Whether the person pressed the control the link landed on. A link completes on a
/// press and a plain open completes nothing.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-015, AUTH-STEP-001, REG-SESS-003 and
/// API-LAND-001.
/// </remarks>
internal sealed record PresentFactorRequest(
    string? ChallengeId,
    Factor Factor,
    string? Value,
    string? LinkToken,
    AuthenticatorAssertion? Assertion,
    bool TrustDevice,
    bool Press);
