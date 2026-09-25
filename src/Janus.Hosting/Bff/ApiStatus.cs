using System.Collections.Frozen;
using System.Collections.Generic;
using Janus.Core;
using Microsoft.AspNetCore.Http;

namespace Janus.Hosting.Bff;

/// <summary>
/// What status each code answers with.
/// </summary>
/// <remarks>
/// Implements API-CONV-003, BFF-ERR-002 and BFF-ERR-003. The mapping is one table
/// rather than a decision taken at each endpoint, so that one failure cannot answer
/// 409 in one place and 422 in another, and so that a code added without a status is
/// a failing test rather than a surprise in production. A code the table does not
/// name answers as a fault, which discloses nothing the table did not decide.
/// </remarks>
internal static class ApiStatus
{
    private static readonly FrozenDictionary<ErrorCode, int> Statuses = new Dictionary<ErrorCode, int>
    {
        // Startup validation never crosses the boundary: a host is refused its model
        // before it serves anything, so arriving here would be a fault. So is a
        // missing policy or a missing derivation source, which 10 calls a fault
        // rather than a denial.
        [ErrorCodes.StartupGoverningLanguage] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupDeclarationMissing] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupPreferenceDeclaration] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupContainmentCycle] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupUnindexedDerivation] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupNoOrganizationPath] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupMissingAssessment] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupUndeclaredTypeReference] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupUndeclaredPermission] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupUndeclaredDerivationReference] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupKeyUnavailable] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupRelyingPartyId] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupLabelLimit] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupRedirectClient] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.StartupSchemaMismatch] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.PolicyUnregistered] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.DerivationSourcesMissing] = StatusCodes.Status500InternalServerError,
        [ErrorCodes.SystemFault] = StatusCodes.Status500InternalServerError,

        // The request itself could not be read, so nothing about the deployment was
        // reached and nothing about it is answered.
        [ErrorCodes.RequestMalformed] = StatusCodes.Status400BadRequest,

        // Session death, and nothing else.
        [ErrorCodes.SessionExpired] = StatusCodes.Status401Unauthorized,

        // Allowed to ask, not allowed to have, and existence is not concealed.
        [ErrorCodes.SessionCsrfInvalid] = StatusCodes.Status403Forbidden,
        [ErrorCodes.StepUpRequired] = StatusCodes.Status403Forbidden,
        [ErrorCodes.StepUpUnavailable] = StatusCodes.Status403Forbidden,
        [ErrorCodes.ChallengeRequired] = StatusCodes.Status403Forbidden,
        [ErrorCodes.Denied] = StatusCodes.Status403Forbidden,
        [ErrorCodes.Restricted] = StatusCodes.Status403Forbidden,
        [ErrorCodes.ConfigurationChangeStepUpRequired] = StatusCodes.Status403Forbidden,
        [ErrorCodes.PolicyGraceExpired] = StatusCodes.Status403Forbidden,
        [ErrorCodes.ConsentRequired] = StatusCodes.Status403Forbidden,
        [ErrorCodes.PhotoNotEnabled] = StatusCodes.Status403Forbidden,

        // Not found, and the concealed denial that answers the same way.
        [ErrorCodes.CredentialNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.GrantNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.DocumentNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.RequestNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.TakedownNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.ErasureNotFound] = StatusCodes.Status404NotFound,
        [ErrorCodes.InvitationNotFound] = StatusCodes.Status404NotFound,

        // A conflict with what is already there, or a precondition the state fails.
        [ErrorCodes.ChangePending] = StatusCodes.Status409Conflict,
        [ErrorCodes.IdentifierPrimary] = StatusCodes.Status409Conflict,
        [ErrorCodes.IdentifierLastOfKind] = StatusCodes.Status409Conflict,
        [ErrorCodes.IdentifierLocked] = StatusCodes.Status409Conflict,
        [ErrorCodes.IdentifierMaximum] = StatusCodes.Status409Conflict,
        [ErrorCodes.UsernameTaken] = StatusCodes.Status409Conflict,
        [ErrorCodes.UsernameReserved] = StatusCodes.Status409Conflict,
        [ErrorCodes.UsernameCoolingOff] = StatusCodes.Status409Conflict,
        [ErrorCodes.MembershipLimitReached] = StatusCodes.Status409Conflict,
        [ErrorCodes.OrganizationProtected] = StatusCodes.Status409Conflict,
        [ErrorCodes.GrantDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.GrantExpired] = StatusCodes.Status409Conflict,
        [ErrorCodes.GroupCycle] = StatusCodes.Status409Conflict,
        [ErrorCodes.GroupInUse] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoleInUse] = StatusCodes.Status409Conflict,
        [ErrorCodes.LossReportPending] = StatusCodes.Status409Conflict,
        [ErrorCodes.LossReportNotPermitted] = StatusCodes.Status409Conflict,
        [ErrorCodes.CredentialNotUpgradable] = StatusCodes.Status409Conflict,
        [ErrorCodes.RequestDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.RequestDecided] = StatusCodes.Status409Conflict,
        [ErrorCodes.ErasureNotFailed] = StatusCodes.Status409Conflict,
        [ErrorCodes.TakedownActive] = StatusCodes.Status409Conflict,
        [ErrorCodes.AccountAdministrativelySuspended] = StatusCodes.Status409Conflict,
        [ErrorCodes.RegistrationSignedIn] = StatusCodes.Status409Conflict,
        [ErrorCodes.NoticeUnpublished] = StatusCodes.Status409Conflict,

        // Well formed, and refused on what it says.
        [ErrorCodes.AffirmationRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ChangeWindowElapsed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.IdentifierInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.IdentifierDomainNotAllowed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.DomainUnverified] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.IdentifierMixedScript] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.InvitationExpired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.InvitationIdentifierMismatch] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ProfileInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ProfileNotAccepted] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ProfileUnderage] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PhotoInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PhotoTooLarge] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RegistrationIncomplete] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.UsernameInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PreferenceUndeclared] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PreferenceWrongType] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PreferenceTooLarge] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PreferenceAdministratorOnly] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CodeExpired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CodeInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CodeReplayed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CredentialLabelInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.FactorNotPermitted] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.FactorRejected] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.FactorRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CredentialSuspended] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.EnrolmentTokenInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RecoveryTokenInvalid] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RecoveryTokenExpired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RecoveryReasonRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RecoveryChannelNotOnAccount] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RecoverySelfApproval] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PasswordBlocklisted] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PasswordTooLong] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PasswordTooShort] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ScreeningUnavailable] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.WebAuthnAlgorithmNotAllowed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.WebAuthnCounterMismatch] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.WebAuthnRelyingPartyChanged] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.WebAuthnUserVerificationRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RestrictionReasonRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.GrantReasonRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConfigurationValueBelowFloor] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConfigurationValueAboveCeiling] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConfigurationValueNotAllowed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConfigurationKeyProtected] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConfigurationLastDestination] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConfigurationPolicyBelowSystem] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.CallbackRejected] = StatusCodes.Status429TooManyRequests,
        [ErrorCodes.EndpointInsecure] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.SmsBalanceFloor] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PurposeNoConsent] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.PurposeNotObjectable] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.NoticeGoverningTextMissing] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RequestReceivedFuture] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConsentSuperseded] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ConsentWrittenRequired] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.DeletionWindowElapsed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.TakedownWindowElapsed] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.ReactivationTokenInvalid] = StatusCodes.Status422UnprocessableEntity,

        // What 10 section 1.2 calls a status and not a refusal: the removal is
        // accepted and the window it takes is what the answer carries, and the
        // held sign-in is answered with what it still needs.
        [ErrorCodes.CredentialLastSecondFactor] = StatusCodes.Status202Accepted,
        [ErrorCodes.DeviceVerificationRequired] = StatusCodes.Status200OK,

        // Throttled, which carries the interval and not the reason. A send a
        // restriction refused is the same answer: 09 gives it 429 wherever it names
        // it, and 10 section 6 reserves 429 for what carries Retry-After.
        [ErrorCodes.Throttled] = StatusCodes.Status429TooManyRequests,
        [ErrorCodes.RestrictionExceeded] = StatusCodes.Status429TooManyRequests,
    }.ToFrozenDictionary();

    /// <summary>
    /// The status a code answers with.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The status.</returns>
    public static int Of(ErrorCode code) =>
        Statuses.TryGetValue(code, out int status)
            ? status
            : StatusCodes.Status500InternalServerError;

    /// <summary>
    /// Whether the table names the code, which every code the library raises is
    /// (BFF-ERR-001 AC3).
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>Whether it is named.</returns>
    public static bool Names(ErrorCode code) => Statuses.ContainsKey(code);
}
