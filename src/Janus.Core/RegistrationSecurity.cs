using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What the security step has set up so far.
/// </summary>
/// <param name="Password">Whether a password is set.</param>
/// <param name="SecondStep">Every second step enrolled, in the order enrolled.</param>
/// <param name="RecoveryCodes">
/// The set drawn by the operation that put a second step beside a password, carried
/// once on that operation's answer and absent everywhere else (AUTH-RECOV-006).
/// </param>
/// <remarks>
/// Implements REG-SESS-006 and AUTH-RECOV-006. The codes leave the library once; the
/// state document read back afterwards carries none.
/// </remarks>
public sealed record RegistrationSecurity(
    bool Password,
    IReadOnlyList<Factor> SecondStep,
    IReadOnlyList<string>? RecoveryCodes);
