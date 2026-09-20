using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Registration;

/// <summary>
/// What the security step has established so far.
/// </summary>
/// <param name="Password">Whether a password is set.</param>
/// <param name="SecondStep">The second steps enrolled.</param>
/// <param name="RecoveryCodes">
/// The codes, returned once at the step that generated them and never again
/// (AUTH-RECOV-006).
/// </param>
/// <remarks>Implements REG-SESS-006.</remarks>
internal sealed record RegistrationSecurityView(
    bool Password,
    IReadOnlyList<Factor> SecondStep,
    IReadOnlyList<string>? RecoveryCodes)
{
    /// <summary>
    /// Reads what the step has established.
    /// </summary>
    /// <param name="security">What the session holds.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The security is absent.</exception>
    public static RegistrationSecurityView Of(RegistrationSecurity security)
    {
        ArgumentNullException.ThrowIfNull(security);

        return new RegistrationSecurityView(
            security.Password,
            security.SecondStep,
            security.RecoveryCodes);
    }
}
