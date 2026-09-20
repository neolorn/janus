using System;
using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.SignIn;

/// <summary>
/// What a sign-in step reached, with what only the browser boundary can act on: the
/// session's secrets and the browser tokens a completed sign-in hands over.
/// </summary>
/// <param name="Progress">What the caller is told.</param>
/// <param name="Session">The session begun, where one was.</param>
/// <param name="Remembered">
/// The token that spares this browser the new-device check, where the check was
/// passed or the sign-in never faced it.
/// </param>
/// <param name="Trusted">
/// The token that spares this browser the second step, where the sign-in asked for
/// the browser to be trusted and the policy permitted it.
/// </param>
/// <remarks>
/// Implements AUTH-FACT-015, AUTH-FACT-016 and LIB-API-005. The contract method is
/// this one without the secrets, because a caller in process has no cookie to write
/// them to.
/// </remarks>
internal sealed record SignInOutcome(
    SignInProgress Progress,
    IssuedSession? Session,
    OpaqueToken? Remembered,
    OpaqueToken? Trusted);
