using System.Collections.Generic;
using Janus.Authentication.Passwords;

namespace Janus.Authentication.Factors;

/// <summary>
/// A set of recovery codes drawn but not yet written: the codes as they are shown,
/// and the hashes that are all the account keeps of them.
/// </summary>
/// <param name="Codes">The codes, in the order they were drawn, shown once.</param>
/// <param name="Hashes">The hashes, in the same order.</param>
/// <remarks>
/// Implements AUTH-FACT-008 and AUTH-RECOV-006. Drawing is separate from writing
/// because the security step of a registration draws them before an account exists
/// to hold them (REG-SESS-002).
/// </remarks>
internal sealed record PreparedRecoveryCodes(
    IReadOnlyList<string> Codes,
    IReadOnlyList<PasswordHash> Hashes);
