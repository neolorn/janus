using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What the security step has established so far.
/// </summary>
/// <param name="Password">Whether a password has been set.</param>
/// <param name="SecondStep">
/// The second steps enrolled against the session, in the order they were enrolled.
/// </param>
/// <remarks>Implements REG-SESS-006 and chapter 9 section 2.</remarks>
public sealed record RegistrationSecurity(
    bool Password,
    IReadOnlyList<Factor> SecondStep);
