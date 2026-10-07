using System;
using Janus.Authentication.Factors;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// A credential as the account lists it.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-008 and AUTH-FACT-001. The one place a stored credential is
/// turned into what a caller is shown, so the list and the answer of an enrolment
/// read the same.
/// </remarks>
internal static class CredentialSummaries
{
    /// <summary>
    /// One credential as it is listed.
    /// </summary>
    /// <param name="credential">The credential.</param>
    /// <param name="preferred">Whether it is the one offered first at a second step.</param>
    /// <returns>What a caller is shown.</returns>
    /// <exception cref="ArgumentNullException">The credential is absent.</exception>
    public static CredentialSummary Of(Authenticator credential, bool preferred)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return new CredentialSummary(
            credential.Id,
            credential.Factor,
            credential.Label.Value,
            credential.State,
            credential.InvalidatesAt,
            credential.WebAuthn?.BackupEligible,
            credential.WebAuthn?.BackupState,
            preferred,
            credential.AddedAt,
            credential.LastUsedAt);
    }
}
