using Janus.Authentication.Sessions;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// What the terms step leaves behind, including what only a browser boundary can
/// use.
/// </summary>
/// <param name="Subject">The account that now exists.</param>
/// <param name="Session">The session it is signed in on, with its two secrets.</param>
/// <param name="Browser">
/// What the registering browser carries so that its next sign-in is not held for a
/// new-device code (AUTH-FACT-016, REG-SESS-007).
/// </param>
/// <remarks>Implements REG-SESS-007, AUTH-FACT-016 and BFF-SESS-001.</remarks>
internal sealed record RegistrationOutcome(
    SubjectId Subject,
    IssuedSession Session,
    OpaqueToken Browser);
