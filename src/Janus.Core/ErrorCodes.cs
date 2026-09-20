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
    /// Startup: the relying party identifier is not a registrable suffix of a
    /// configured origin. Name an identifier every origin sits under, or leave it
    /// unset and let the common parent domain stand.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-010, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupRelyingPartyId { get; } = ErrorCode.Parse("model.startup.rpid");

    /// <summary>
    /// Startup: the configured origins exceed the five-label limit a browser admits
    /// in a related-origins allowlist. Serve fewer domains from one relying party.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-012, chapter 10 section 1.5.</remarks>
    public static ErrorCode StartupLabelLimit { get; } = ErrorCode.Parse("model.startup.labellimit");

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
    /// The affirmation derived at the age step is absent, so the terms step has nothing
    /// to record. Answer the age step and repeat the terms step.
    /// </summary>
    /// <remarks>Implements REG-PROF-002, REG-SESS-007, chapter 10 section 1.1.</remarks>
    public static ErrorCode AffirmationRequired { get; } = ErrorCode.Parse("identity.affirmation.required");

    /// <summary>
    /// A change of this kind is in progress already: a replace is staged for the
    /// identifier. Complete or abandon that one first.
    /// </summary>
    /// <remarks>Implements REG-IDENT-007, chapter 10 section 1.1.</remarks>
    public static ErrorCode ChangePending { get; } = ErrorCode.Parse("identity.change.pending");

    /// <summary>
    /// The undo of an identifier removal or replace arrived after
    /// <c>identifier.change.coolingoff</c>. Add the identifier again as a new one.
    /// </summary>
    /// <remarks>Implements REG-IDENT-006, REG-IDENT-007, chapter 10 section 1.1.</remarks>
    public static ErrorCode ChangeWindowElapsed { get; } = ErrorCode.Parse("identity.change.windowelapsed");

    /// <summary>
    /// The identifier is the primary of its kind, which is not removable. Set another
    /// primary first, then remove it.
    /// </summary>
    /// <remarks>Implements REG-IDENT-006, chapter 10 section 1.1.</remarks>
    public static ErrorCode IdentifierPrimary { get; } = ErrorCode.Parse("identity.identifier.primary");

    /// <summary>
    /// The value is not a well-formed identifier of its kind. Enter an address or a
    /// number the deployment stores.
    /// </summary>
    /// <remarks>Implements REG-IDENT-001, chapter 10 section 1.1.</remarks>
    public static ErrorCode IdentifierInvalid { get; } = ErrorCode.Parse("identity.identifier.invalid");

    /// <summary>
    /// The identifier is locked: an invitation bound it, or a provider operates the
    /// mailbox. Nothing about it is the person's to change.
    /// </summary>
    /// <remarks>Implements REG-IDENT-010, chapter 10 section 1.1.</remarks>
    public static ErrorCode IdentifierLocked { get; } = ErrorCode.Parse("identity.identifier.locked");

    /// <summary>
    /// The account or the registration holds as many of the kind as it may. Remove one
    /// of them first, or, where the maximum is one, replace it in one operation.
    /// </summary>
    /// <remarks>Implements REG-IDENT-002, REG-IDENT-007, chapter 10 section 1.1.</remarks>
    public static ErrorCode IdentifierMaximum { get; } = ErrorCode.Parse("identity.identifier.maximum");

    /// <summary>
    /// Removal would leave fewer than the required minimum of the kind. Add another of
    /// the kind and verify it first.
    /// </summary>
    /// <remarks>Implements REG-IDENT-001, REG-IDENT-006, chapter 10 section 1.1.</remarks>
    public static ErrorCode IdentifierLastOfKind { get; } = ErrorCode.Parse("identity.identifier.lastofkind");

    /// <summary>
    /// Scripts are mixed within a single word of the value. Write each word in one
    /// script.
    /// </summary>
    /// <remarks>Implements IDN-ACCT-005, chapter 10 section 1.1.</remarks>
    public static ErrorCode IdentifierMixedScript { get; } = ErrorCode.Parse("identity.identifier.mixedscript");

    /// <summary>
    /// The step the request is for is not the step the registration has reached: its
    /// predecessor is incomplete, or it is complete already. Read the session's state
    /// and answer the step it names.
    /// </summary>
    /// <remarks>Implements REG-SESS-002, REG-SESS-004, chapter 10 section 1.1.</remarks>
    public static ErrorCode RegistrationIncomplete { get; } = ErrorCode.Parse("identity.registration.incomplete");

    /// <summary>
    /// The date of birth is under eighteen where the deployment takes an adult
    /// affirmation. The registration session has ended; nothing further is accepted in
    /// it.
    /// </summary>
    /// <remarks>Implements REG-PROF-002, chapter 10 section 1.1.</remarks>
    public static ErrorCode ProfileUnderage { get; } = ErrorCode.Parse("identity.profile.underage");

    /// <summary>
    /// A profile field is not one the library admits: a display name over its byte
    /// bound, or a legal name over its length. Shorten it.
    /// </summary>
    /// <remarks>Implements REG-PROF-001, chapter 10 section 1.1.</remarks>
    public static ErrorCode ProfileInvalid { get; } = ErrorCode.Parse("identity.profile.invalid");

    /// <summary>
    /// The deployment does not take the field from the person: its key is off, or it
    /// is the date of birth, which is corrected through support and nowhere else. Leave
    /// the field out and send the rest.
    /// </summary>
    /// <remarks>Implements REG-PROF-001, REG-IDENT-009, chapter 10 section 1.1.</remarks>
    public static ErrorCode ProfileNotAccepted { get; } = ErrorCode.Parse("identity.profile.notaccepted");

    /// <summary>
    /// A second username change fell inside <c>identifiers.username.changecooloff</c>.
    /// Repeat it after the end the details carry.
    /// </summary>
    /// <remarks>Implements REG-IDENT-009, chapter 10 section 1.1.</remarks>
    public static ErrorCode UsernameCoolingOff { get; } = ErrorCode.Parse("identity.username.coolingoff");

    /// <summary>
    /// The username is on the reserved list. Choose another.
    /// </summary>
    /// <remarks>Implements REG-IDENT-009, chapter 10 section 1.1.</remarks>
    public static ErrorCode UsernameReserved { get; } = ErrorCode.Parse("identity.username.reserved");

    /// <summary>
    /// The username belongs to another account, or is held after an erasure. Choose
    /// another.
    /// </summary>
    /// <remarks>Implements REG-IDENT-009, chapter 10 section 1.1.</remarks>
    public static ErrorCode UsernameTaken { get; } = ErrorCode.Parse("identity.username.taken");

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
    /// than waived. Register an assurance provider, or bind the action to no gate.
    /// </summary>
    /// <remarks>Implements AUTH-STEP-003, LIB-HOST-004, chapter 10 section 1.2.</remarks>
    public static ErrorCode StepUpUnavailable { get; } = ErrorCode.Parse("auth.stepup.unavailable");

    /// <summary>
    /// The verification code is past its lifetime. Ask for another, within the sending
    /// restrictions.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-004, chapter 10 section 1.2.</remarks>
    public static ErrorCode CodeExpired { get; } = ErrorCode.Parse("auth.code.expired");

    /// <summary>
    /// The verification code is not the one that was sent. Enter the one from the
    /// message.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-004, chapter 10 section 1.2.</remarks>
    public static ErrorCode CodeInvalid { get; } = ErrorCode.Parse("auth.code.invalid");

    /// <summary>
    /// The code was already consumed inside its own window. Wait for the next one.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-005, chapter 10 section 1.2.</remarks>
    public static ErrorCode CodeReplayed { get; } = ErrorCode.Parse("auth.code.replayed");

    /// <summary>
    /// The factor is not one the principal's policy admits, or it proves control of a
    /// channel and never authenticates. Present one the policy lists.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-002, chapter 10 section 1.2.</remarks>
    public static ErrorCode FactorNotPermitted { get; } = ErrorCode.Parse("auth.factor.notpermitted");

    /// <summary>
    /// The factor was presented and refused. Present it again, or present another
    /// the policy admits.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-001, chapter 10 section 1.2.</remarks>
    public static ErrorCode FactorRejected { get; } = ErrorCode.Parse("auth.factor.rejected");

    /// <summary>
    /// Further factors are needed to reach the assurance required. Present one of the
    /// combinations offered.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-001, chapter 10 section 1.2.</remarks>
    public static ErrorCode FactorRequired { get; } = ErrorCode.Parse("auth.factor.required");

    /// <summary>
    /// The account holds no such credential. Name one the credential list carries.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-001, chapter 10 section 1.2.</remarks>
    public static ErrorCode CredentialNotFound { get; } = ErrorCode.Parse("auth.credential.notfound");

    /// <summary>
    /// The label is empty, longer than the bound, or already held by another
    /// credential of the same kind on the account. Choose another.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-001, chapter 10 section 1.2.</remarks>
    public static ErrorCode CredentialLabelInvalid { get; } =
        ErrorCode.Parse("auth.credential.labelinvalid");

    /// <summary>
    /// The password matched one of the sources the deployment rejects on. Choose
    /// another; length does not excuse a match.
    /// </summary>
    /// <remarks>Implements AUTH-PASS-004, chapter 10 section 1.2.</remarks>
    public static ErrorCode PasswordBlocklisted { get; } = ErrorCode.Parse("auth.password.blocklisted");

    /// <summary>
    /// The password is below the floor that applies to it. The shorter floor is reached
    /// by holding a second factor, not by choosing it.
    /// </summary>
    /// <remarks>Implements AUTH-PASS-001, chapter 10 section 1.2.</remarks>
    public static ErrorCode PasswordTooShort { get; } = ErrorCode.Parse("auth.password.tooshort");

    /// <summary>
    /// No source the deployment screens against could answer, so the password was not
    /// screened and the operation is refused rather than accepted unscreened. Restore
    /// a corpus and repeat the operation.
    /// </summary>
    /// <remarks>Implements AUTH-PASS-004, chapter 10 section 1.2.</remarks>
    public static ErrorCode ScreeningUnavailable { get; } = ErrorCode.Parse("auth.screening.unavailable");

    /// <summary>
    /// The session is past its idle or its absolute limit. The details say whether one
    /// factor restores it or a full authentication is required.
    /// </summary>
    /// <remarks>Implements AUTH-SESS-005, chapter 10 section 1.2.</remarks>
    public static ErrorCode SessionExpired { get; } = ErrorCode.Parse("auth.session.expired");

    /// <summary>
    /// The cross-site request forgery token is absent or was rejected. Obtain the
    /// token the session carries and repeat the request.
    /// </summary>
    /// <remarks>Implements AUTH-SESS-007, chapter 10 section 1.2.</remarks>
    public static ErrorCode SessionCsrfInvalid { get; } = ErrorCode.Parse("auth.session.csrfinvalid");

    /// <summary>
    /// The sign-in is held until the code sent to the account's primary email is
    /// entered. A status, not a refusal.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-016, chapter 10 section 1.2.</remarks>
    public static ErrorCode DeviceVerificationRequired { get; } = ErrorCode.Parse("auth.device.verificationrequired");

    /// <summary>
    /// The account does not meet a raised requirement and the run-up has elapsed. The
    /// sign-in stops at enrolment.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-017, chapter 10 section 1.2.</remarks>
    public static ErrorCode PolicyGraceExpired { get; } = ErrorCode.Parse("auth.policy.graceexpired");

    /// <summary>
    /// The credential's signature algorithm is outside the allow-list. Enrol an
    /// authenticator that produces one the deployment admits.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-014, chapter 10 section 1.2.</remarks>
    public static ErrorCode WebAuthnAlgorithmNotAllowed { get; } = ErrorCode.Parse("auth.webauthn.algorithmnotallowed");

    /// <summary>
    /// The signature counter moved backwards, which is what a cloned credential looks
    /// like. Remove the credential and enrol again.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-014, chapter 10 section 1.2.</remarks>
    public static ErrorCode WebAuthnCounterMismatch { get; } = ErrorCode.Parse("auth.webauthn.countermismatch");

    /// <summary>
    /// The credential was enrolled under a different relying party identifier. Enrol
    /// again under the one in force.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-011, chapter 10 section 1.2.</remarks>
    public static ErrorCode WebAuthnRelyingPartyChanged { get; } = ErrorCode.Parse("auth.webauthn.rpidchanged");

    /// <summary>
    /// User verification did not occur. Present the credential with the gesture the
    /// authenticator asks for.
    /// </summary>
    /// <remarks>Implements AUTH-FACT-014, chapter 10 section 1.2.</remarks>
    public static ErrorCode WebAuthnUserVerificationRequired { get; } = ErrorCode.Parse("auth.webauthn.userverificationrequired");

    /// <summary>
    /// A progressive delay is in force after repeated failures. Wait the period the
    /// response states and try again; nothing about the account has changed.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-001, AUTH-ABUSE-002, chapter 10 section 1.2.</remarks>
    public static ErrorCode Throttled { get; } = ErrorCode.Parse("auth.throttled");

    /// <summary>
    /// A named restriction refused the send. The details carry <c>retryAt</c>, the
    /// earliest time a bucket lifts; wait until then or ask support for credit.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-004, AUTH-ABUSE-002, chapter 10 section 1.2.</remarks>
    public static ErrorCode RestrictionExceeded { get; } = ErrorCode.Parse("auth.restriction.exceeded");

    /// <summary>
    /// A restriction grant, or an edit that loosens a restriction, arrived without a
    /// written reason. State the reason and submit it again.
    /// </summary>
    /// <remarks>Implements AUTH-ABUSE-004, OPS-CFG-002, chapter 10 section 1.2.</remarks>
    public static ErrorCode RestrictionReasonRequired { get; } = ErrorCode.Parse("auth.restriction.reasonrequired");

    /// <summary>
    /// Permission is absent, on something whose existence is not concealed. Hold the
    /// permission, or ask someone who does.
    /// </summary>
    /// <remarks>Implements AUTHZ-CONCEAL-005, chapter 10 section 1.3.</remarks>
    public static ErrorCode Denied { get; } = ErrorCode.Parse("authz.denied");

    /// <summary>
    /// A check or a capability query was made on a type that declares a derivation
    /// without the host-supplied sources it is evaluated over. Pass the same sources
    /// the filter takes.
    /// </summary>
    /// <remarks>
    /// Implements AUTHZ-DERIVE-001, D-161, chapter 10 section 1.3. A fault and not a
    /// denial: the caller asked a question the library cannot answer, rather than one
    /// whose answer is no.
    /// </remarks>
    public static ErrorCode DerivationSourcesMissing { get; } =
        ErrorCode.Parse("authz.derivation.sourcesmissing");

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
    /// A callback carried an unknown correlation reference, or arrived faster than the
    /// callback rate allows. Call again with the reference the send returned.
    /// </summary>
    /// <remarks>Implements INT-GEN-003, AUTH-ABUSE-007, chapter 10 section 1.6.</remarks>
    public static ErrorCode CallbackRejected { get; } = ErrorCode.Parse("integration.callback.rejected");

    /// <summary>
    /// Startup: an integration endpoint is not TLS. The details name the integration
    /// under <c>integration</c> and the setting under <c>key</c>; give it an
    /// <c>https</c> endpoint.
    /// </summary>
    /// <remarks>Implements INT-GEN-001, chapter 10 section 1.6.</remarks>
    public static ErrorCode EndpointInsecure { get; } = ErrorCode.Parse("integration.endpoint.insecure");

    /// <summary>
    /// The gateway balance is below the configured floor, so ordinary sends are
    /// refused. Top the account up; alert-class messages continue meanwhile.
    /// </summary>
    /// <remarks>Implements INT-SMS-004, AUTH-ABUSE-006, chapter 10 section 1.6.</remarks>
    public static ErrorCode SmsBalanceFloor { get; } = ErrorCode.Parse("integration.sms.balancefloor");

    /// <summary>
    /// An unhandled fault. The body carries the correlation identifier and nothing
    /// else; quote it when reporting the fault.
    /// </summary>
    /// <remarks>Implements BFF-ERR-002, chapter 10 sections 1.5 and 6.</remarks>
    public static ErrorCode SystemFault { get; } = ErrorCode.Parse("system.fault");
}
