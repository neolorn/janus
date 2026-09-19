namespace Janus.Core;

/// <summary>
/// The error codes of chapter 10 section 1 that the library raises. Each is stable:
/// rewording the message a host renders from a code is free, changing the code is a
/// breaking change to the contract.
/// </summary>
/// <remarks>Implements CONV-NAME-003, LIB-API-001.</remarks>
public static class ErrorCodes
{
    /// <summary>
    /// The setting is not changeable through the application. Change it where the
    /// application cannot reach, by a command on the server or by a redeployment.
    /// </summary>
    /// <remarks>Implements OPS-CFG-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationKeyProtected { get; } = ErrorCode.Parse("config.key.protected");

    /// <summary>
    /// The value is below the enforced minimum. Supply a value at or above the floor;
    /// the floor is not clamped to silently.
    /// </summary>
    /// <remarks>Implements OPS-CFG-003, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationValueBelowFloor { get; } = ErrorCode.Parse("config.value.belowfloor");

    /// <summary>
    /// The value is above the enforced maximum. Supply a value at or below the
    /// ceiling.
    /// </summary>
    /// <remarks>Implements AUTH-SESS-005, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationValueAboveCeiling { get; } = ErrorCode.Parse("config.value.aboveceiling");

    /// <summary>
    /// The value is outside the key's stated set, or is not of the key's type. Supply
    /// one of the values the key admits.
    /// </summary>
    /// <remarks>Implements chapter 10 section 4 value types, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationValueNotAllowed { get; } = ErrorCode.Parse("config.value.notallowed");

    /// <summary>
    /// Loosening a control requires step-up authentication and a written reason.
    /// Present both and repeat the change.
    /// </summary>
    /// <remarks>Implements OPS-CFG-002, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationChangeStepUpRequired { get; } = ErrorCode.Parse("config.change.stepuprequired");

    /// <summary>
    /// An organization policy field is looser than the system default. Tighten the
    /// field, or raise the system default first.
    /// </summary>
    /// <remarks>Implements AUTH-STEP-002a, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationPolicyBelowSystem { get; } = ErrorCode.Parse("config.policy.belowsystem");

    /// <summary>
    /// Startup: the governing language of legal documents is unset. Supply
    /// <c>legal.governinglanguage</c>; it has no default and cannot be guessed for a
    /// deployment.
    /// </summary>
    /// <remarks>Implements PRIV-CONS-005, LIB-HOST-001, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupGoverningLanguage { get; } = ErrorCode.Parse("model.startup.governinglanguage");

    /// <summary>
    /// Startup: a value the deployment has to name, a subject-event handler or a
    /// restriction key supplier is absent. The details name it under <c>key</c>,
    /// <c>handler</c> or <c>supplier</c>; supply it.
    /// </summary>
    /// <remarks>Implements LIB-HOST-001, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupDeclarationMissing { get; } = ErrorCode.Parse("model.startup.declarationmissing");

    /// <summary>
    /// Startup: a host preference declaration is malformed. The details name the key
    /// under <c>preference</c>; correct the declaration.
    /// </summary>
    /// <remarks>Implements REG-PREF-001, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupPreferenceDeclaration { get; } = ErrorCode.Parse("model.startup.preferencedeclaration");

    /// <summary>
    /// Startup: the containment declaration forms a cycle. Break the cycle; a type
    /// cannot be contained, at any depth, by a type it contains.
    /// </summary>
    /// <remarks>Implements AUTHZ-MODEL-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupContainmentCycle { get; } = ErrorCode.Parse("model.containment.cycle");

    /// <summary>
    /// Startup: a derivation names a column that carries no index. Index the column or
    /// drop the derivation.
    /// </summary>
    /// <remarks>Implements AUTHZ-DERIVE-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupUnindexedDerivation { get; } = ErrorCode.Parse("model.derivation.unindexed");

    /// <summary>
    /// Startup: a resource type reaches no organization through its containment.
    /// Contain it, at some depth, in a type that names the organization it belongs to.
    /// </summary>
    /// <remarks>Implements AUTHZ-MODEL-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupNoOrganizationPath { get; } = ErrorCode.Parse("model.type.noorganizationpath");

    /// <summary>
    /// Startup: a purpose rests on a basis that requires an assessment and names
    /// none. Name the legitimate interest assessment, or rest the purpose elsewhere.
    /// </summary>
    /// <remarks>Implements PRIV-BASIS-002, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupMissingAssessment { get; } = ErrorCode.Parse("model.purpose.missingassessment");

    /// <summary>
    /// Startup: a resource type references a type the model does not declare. Declare
    /// the referenced type or drop the reference.
    /// </summary>
    /// <remarks>Implements AUTHZ-MODEL-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupUndeclaredTypeReference { get; } = ErrorCode.Parse("model.type.undeclaredreference");

    /// <summary>
    /// Startup: a role grants a permission the model does not declare. Declare the
    /// permission or drop it from the role.
    /// </summary>
    /// <remarks>Implements AUTHZ-MODEL-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupUndeclaredPermission { get; } = ErrorCode.Parse("model.role.undeclaredpermission");

    /// <summary>
    /// Startup: a derivation references a type or relationship the model does not
    /// declare. Declare it or drop the derivation.
    /// </summary>
    /// <remarks>Implements AUTHZ-MODEL-004, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupUndeclaredDerivationReference { get; } = ErrorCode.Parse("model.derivation.undeclaredreference");

    /// <summary>
    /// The organization named is the administrative one, which is not deletable.
    /// Delete another organization, or none.
    /// </summary>
    /// <remarks>Implements IDN-ORG-004, chapter 10 section 1.1.</remarks>
    public static ErrorCode OrganizationProtected { get; } = ErrorCode.Parse("identity.organization.protected");

    /// <summary>
    /// The username fails the PRECIS UsernameCaseMapped profile, its length bounds, or
    /// holds no letter. Choose one the profile admits that is not all digits.
    /// </summary>
    /// <remarks>Implements REG-IDENT-009, chapter 10 section 1.1.</remarks>
    public static ErrorCode UsernameInvalid { get; } = ErrorCode.Parse("identity.username.invalid");

    /// <summary>
    /// The preference key is one the host never declared. Send a key the deployment
    /// declares.
    /// </summary>
    /// <remarks>Implements REG-PREF-001, chapter 10 section 1.1.</remarks>
    public static ErrorCode PreferenceUndeclared { get; } = ErrorCode.Parse("identity.preference.undeclared");

    /// <summary>
    /// A preference value is of a type other than its declaration. Send a value of the
    /// declared type.
    /// </summary>
    /// <remarks>Implements REG-PREF-001, chapter 10 section 1.1.</remarks>
    public static ErrorCode PreferenceWrongType { get; } = ErrorCode.Parse("identity.preference.wrongtype");

    /// <summary>
    /// The preference set would exceed <c>preferences.maxsize</c>. Remove or shorten a
    /// value.
    /// </summary>
    /// <remarks>Implements REG-PREF-001, chapter 10 section 1.1.</remarks>
    public static ErrorCode PreferenceTooLarge { get; } = ErrorCode.Parse("identity.preference.toolarge");

    /// <summary>
    /// The preference is declared administrator-only and the person is not one. Ask an
    /// administrator to set it.
    /// </summary>
    /// <remarks>Implements REG-PREF-001, chapter 10 section 1.1.</remarks>
    public static ErrorCode PreferenceAdministratorOnly { get; } = ErrorCode.Parse("identity.preference.administratoronly");

    /// <summary>
    /// A bot-defence signal fired and the host declared a challenge verifier. Present a
    /// passing challenge token and repeat the step.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-008, chapter 10 section 1.5.</remarks>
    public static ErrorCode ChallengeRequired { get; } = ErrorCode.Parse("auth.challenge.required");

    /// <summary>
    /// The step-up gate bound to the action is not met. Present what the gate asks
    /// for and repeat the operation.
    /// </summary>
    /// <remarks>Implements AUTH-STEP-001, AUTH-STEP-002, chapter 10 section 1.2.</remarks>
    public static ErrorCode StepUpRequired { get; } = ErrorCode.Parse("auth.stepup.required");

    /// <summary>
    /// The action is bound to a step-up gate and no assurance provider is registered,
    /// so nothing reports what the session has proved and the gate is unmet rather
    /// than waived.
    /// </summary>
    /// <remarks>Implements AUTH-STEP-003, LIB-HOST-004, chapter 10 section 1.2.</remarks>
    public static ErrorCode StepUpUnavailable { get; } = ErrorCode.Parse("auth.stepup.unavailable");

    /// <summary>
    /// Permission is absent, on something whose existence is not concealed. Hold the
    /// permission, or ask someone who does.
    /// </summary>
    /// <remarks>Implements AUTHZ-CONCEAL-005, chapter 10 section 1.3.</remarks>
    public static ErrorCode Denied { get; } = ErrorCode.Parse("authz.denied");

    /// <summary>
    /// An identical live grant exists. Revoke it, or change what this one says.
    /// </summary>
    /// <remarks>Implements AUTHZ-GRANT-003, chapter 10 section 1.3.</remarks>
    public static ErrorCode GrantDuplicate { get; } = ErrorCode.Parse("authz.grant.duplicate");

    /// <summary>
    /// The grant is past its expiry and confers nothing. Grant it again, with an
    /// expiry that has not passed.
    /// </summary>
    /// <remarks>Implements AUTHZ-GRANT-003, chapter 10 section 1.3.</remarks>
    public static ErrorCode GrantExpired { get; } = ErrorCode.Parse("authz.grant.expired");

    /// <summary>
    /// No such grant. Name a grant that exists.
    /// </summary>
    /// <remarks>Implements AUTHZ-GRANT-001, chapter 10 section 1.3.</remarks>
    public static ErrorCode GrantNotFound { get; } = ErrorCode.Parse("authz.grant.notfound");

    /// <summary>
    /// A grant was created or revoked without a reason. Supply a non-empty reason.
    /// </summary>
    /// <remarks>Implements AUTHZ-GRANT-003, chapter 10 section 1.3.</remarks>
    public static ErrorCode GrantReasonRequired { get; } = ErrorCode.Parse("authz.grant.reasonrequired");

    /// <summary>
    /// Adding the member would make a group contain itself. Add it somewhere the
    /// group does not already reach.
    /// </summary>
    /// <remarks>Implements AUTHZ-GROUP-001, chapter 10 section 1.3.</remarks>
    public static ErrorCode GroupCycle { get; } = ErrorCode.Parse("authz.group.cycle");

    /// <summary>
    /// The entity has no registered policy, which is a fault rather than a denial.
    /// Register a policy for the entity in the model.
    /// </summary>
    /// <remarks>Implements AUTHZ-GATE-001, chapter 10 section 1.3.</remarks>
    public static ErrorCode PolicyUnregistered { get; } = ErrorCode.Parse("authz.policy.unregistered");

    /// <summary>
    /// The subject's processing is restricted, so the record is readable and not
    /// modifiable. Lift the restriction first.
    /// </summary>
    /// <remarks>Implements AUTHZ-GATE-006, chapter 10 section 1.3.</remarks>
    public static ErrorCode Restricted { get; } = ErrorCode.Parse("authz.restricted");

    /// <summary>
    /// The change would leave an alert destination list empty. Add a destination
    /// before removing the last one.
    /// </summary>
    /// <remarks>Implements OPS-ALERT-004a, chapter 10 section 1.5.</remarks>
    public static ErrorCode ConfigurationLastDestination { get; } = ErrorCode.Parse("config.value.lastdestination");

    /// <summary>
    /// An unhandled fault. The body carries the correlation identifier and nothing
    /// else; quote it when reporting the fault.
    /// </summary>
    /// <remarks>Implements BFF-ERR-002, chapter 10 sections 1.5 and 6.</remarks>
    public static ErrorCode SystemFault { get; } = ErrorCode.Parse("system.fault");
}
