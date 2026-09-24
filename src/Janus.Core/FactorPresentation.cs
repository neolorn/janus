using System;

namespace Janus.Core;

/// <summary>
/// One factor, as it is presented at a sign-in or at a gate.
/// </summary>
/// <param name="Factor">Which factor is presented.</param>
/// <remarks>
/// Implements AUTH-FACT-001 and AUTH-FACT-015. A factor carries either a typed value
/// or an authenticator's answer, never both; which one it carries follows from the
/// kind and not from what the caller chose to fill in.
/// </remarks>
public sealed record FactorPresentation(Factor Factor)
{
    /// <summary>
    /// What was typed: a password, a code, a recovery code, or the token a link
    /// carried.
    /// </summary>
    [NeverLogged]
    public string? Value { get; init; }

    /// <summary>What the authenticator answered, for a WebAuthn factor.</summary>
    public AuthenticatorAssertion? Assertion { get; init; }

    /// <summary>
    /// Whether the browser is to be trusted, offered only on the call that completes
    /// a two-factor sign-in and only where the policy permits it.
    /// </summary>
    public bool TrustDevice { get; init; }
}
