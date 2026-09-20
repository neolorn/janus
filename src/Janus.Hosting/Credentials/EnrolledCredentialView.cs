using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Credentials;

/// <summary>
/// What an enrolment leaves behind: the credential, whether a second one is asked
/// for, and the codes the enrolment brought with it.
/// </summary>
/// <param name="Id">Which credential was enrolled.</param>
/// <param name="SecondCredential">
/// Whether a second credential is asked for, and whether it may be declined.
/// </param>
/// <param name="RecoveryCodes">The codes, shown once and never read back.</param>
/// <remarks>Implements AUTH-RECOV-001 and AUTH-RECOV-006.</remarks>
internal sealed record EnrolledCredentialView(
    Guid Id,
    CredentialRedundancy? SecondCredential,
    IReadOnlyList<string>? RecoveryCodes)
{
    /// <summary>
    /// Reads an enrolment.
    /// </summary>
    /// <param name="enrolled">What the enrolment left behind.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The enrolment is absent.</exception>
    public static EnrolledCredentialView Of(EnrolledCredential enrolled)
    {
        ArgumentNullException.ThrowIfNull(enrolled);

        return new EnrolledCredentialView(
            enrolled.Credential.Value,
            enrolled.SecondCredential,
            enrolled.RecoveryCodes);
    }
}
