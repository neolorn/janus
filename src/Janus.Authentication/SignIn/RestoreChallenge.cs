using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.SignIn;

/// <summary>
/// What the restore in place of a session that lapsed for inactivity begins with.
/// </summary>
/// <param name="Challenge">The handle the restore presents.</param>
/// <param name="Factors">
/// Each of the passkey and the password the account holds and the policy in force
/// permits, which are what may restore the session.
/// </param>
/// <param name="WebAuthn">
/// The request options, naming each active passkey of the account.
/// </param>
/// <remarks>
/// Implements AUTH-SESS-005 and D-224 item 13 (`09` <c>POST /auth/restore/begin</c>).
/// </remarks>
internal sealed record RestoreChallenge(
    string Challenge,
    IReadOnlyList<Factor> Factors,
    WebAuthnRequestOptions WebAuthn);
