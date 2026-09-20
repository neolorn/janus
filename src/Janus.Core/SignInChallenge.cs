using System;
using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a sign-in begins with: the handle the later steps present, the factors the
/// policy admits and the WebAuthn challenge.
/// </summary>
/// <param name="Challenge">The handle, presented at every later step.</param>
/// <param name="Available">
/// The policy's enabled primary factors, never the account's, so that the answer is
/// identical for every identifier, existing or not.
/// </param>
/// <param name="WebAuthn">The assertion challenge, always present.</param>
/// <remarks>
/// Implements AUTH-ABUSE-003 and AUTH-FACT-002. Second-factor requirements are not
/// disclosed here: they surface only after a first factor succeeds (`09` section 3).
/// </remarks>
public sealed record SignInChallenge(
    string Challenge,
    IReadOnlyList<Factor> Available,
    WebAuthnChallenge WebAuthn);
