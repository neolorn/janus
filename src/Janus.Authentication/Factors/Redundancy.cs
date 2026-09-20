using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Authentication.Factors;

/// <summary>
/// Whether an account holds a credential it would still have after losing a device.
/// </summary>
/// <remarks>
/// Implements AUTH-RECOV-001. The question is answered from what the credentials say
/// about themselves and never from what made them: a device-bound passkey on a lost
/// device is gone, whoever made it.
/// </remarks>
internal static class Redundancy
{
    /// <summary>
    /// Whether the account needs no second credential.
    /// </summary>
    /// <param name="enrolled">Every credential the account holds.</param>
    /// <returns>Whether it is satisfied.</returns>
    public static bool Satisfied(IReadOnlyList<Authenticator> enrolled) =>
        enrolled.Count(credential => credential.IsUsable) > 1
        || enrolled.Any(credential => credential.IsUsable && credential.WebAuthn?.BackupState is true);
}
