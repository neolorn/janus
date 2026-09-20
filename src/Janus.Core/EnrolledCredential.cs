using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What an enrolment leaves behind: the credential, whether the account is now asked
/// for a second one, and the recovery codes a second step beside a password brings
/// with it.
/// </summary>
/// <param name="Credential">Which credential was enrolled.</param>
/// <param name="SecondCredential">
/// Nothing where the credential is synced and stands alone, and otherwise whether the
/// second one the account is asked for may be declined (AUTH-RECOV-001).
/// </param>
/// <param name="RecoveryCodes">
/// The set generated because a second step was enrolled beside a password, returned
/// once and never read back, and nothing where the enrolment brought none
/// (AUTH-RECOV-006).
/// </param>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-013, AUTH-RECOV-001 and AUTH-RECOV-006. The
/// second-credential prompt reads <c>backupState</c> and never the authenticator's
/// make: a device-bound credential on a lost device is gone, whoever made it.
/// </remarks>
public sealed record EnrolledCredential(
    AuthenticatorId Credential,
    CredentialRedundancy? SecondCredential,
    IReadOnlyList<string>? RecoveryCodes);
