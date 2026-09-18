using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Janus.Core.Configuration;

/// <summary>
/// The configuration keys of chapter 10 section 4, each with the type, the default
/// and the constraints the chapter declares for it. Names, types and constraints are
/// part of the stable public contract.
/// </summary>
/// <remarks>
/// Implements chapter 10 section 4, LIB-API-001, LIB-HOST-001, OPS-CFG-001,
/// OPS-CFG-003, OPS-CFG-004. Where the chapter does not state which way a change
/// loosens the deployment, the key is <see cref="SettingDirection.AnyChange"/>, so
/// every change to it carries the friction of a loosening (OPS-CFG-002, D-079b).
/// </remarks>
public static class Settings
{
    // The four restrictions the library ships, from section 4.5. A host edits them
    // through the restriction operations; it does not replace this list wholesale.
    private static IReadOnlyList<Restriction> ShippedRestrictions { get; } =
    [
        new(
            "sms.destination",
            RestrictionKeyKind.Destination,
            HostKeyName: null,
            RestrictionPurpose.Any,
            [new Bucket(3, TimeSpan.FromHours(24), BucketWindow.Sliding)]),
        new(
            "sms.source",
            RestrictionKeyKind.Source,
            HostKeyName: null,
            RestrictionPurpose.Any,
            [new Bucket(10, TimeSpan.FromHours(1), BucketWindow.Sliding)]),
        new(
            "email.destination",
            RestrictionKeyKind.Destination,
            HostKeyName: null,
            RestrictionPurpose.Any,
            [
                new Bucket(5, TimeSpan.FromHours(1), BucketWindow.Sliding),
                new Bucket(1, TimeSpan.FromSeconds(60), BucketWindow.Fixed),
            ]),
        new(
            "notification.destination",
            RestrictionKeyKind.Destination,
            HostKeyName: null,
            RestrictionPurpose.Notification,
            [new Bucket(5, TimeSpan.FromHours(24), BucketWindow.Sliding)]),
    ];

    /// <summary>How long a session may sit idle where the policy requires AAL2.</summary>
    public static DurationSetting SessionAal2Inactivity { get; } =
        new("session.aal2.inactivity", SettingScope.Runtime, SettingDirection.Increase, TimeSpan.FromHours(1), ceiling: TimeSpan.FromHours(12));

    /// <summary>How long a session may live where the policy requires AAL2.</summary>
    public static DurationSetting SessionAal2Absolute { get; } =
        new("session.aal2.absolute", SettingScope.Runtime, SettingDirection.Increase, TimeSpan.FromHours(24), ceiling: TimeSpan.FromHours(24));

    /// <summary>How long a session under the system policy may sit idle, refreshed by use.</summary>
    public static DurationSetting SessionDefaultInactivity { get; } =
        new("session.default.inactivity", SettingScope.Runtime, SettingDirection.Increase, TimeSpan.FromDays(90), ceiling: TimeSpan.FromDays(365));

    /// <summary>The definite overall timeout of a session under the system policy.</summary>
    public static DurationSetting SessionDefaultAbsolute { get; } =
        new("session.default.absolute", SettingScope.Runtime, SettingDirection.Increase, TimeSpan.FromDays(365), ceiling: TimeSpan.FromDays(365));

    /// <summary>How recently the factors that satisfy a gate have to have been presented.</summary>
    public static DurationSetting SessionStepUpRecency { get; } =
        new("session.stepup.recency", SettingScope.Runtime, SettingDirection.Increase, Policies.StepUpRecency);

    /// <summary>The system policy, for principals with no membership.</summary>
    public static PolicySetting PolicyDefault { get; } =
        new("policy.default", SettingScope.Runtime, SettingDirection.AnyChange, Policies.SystemDefault);

    /// <summary>Subject exports one account may ask for in a day.</summary>
    public static IntegerSetting PrivacyExportRateLimit { get; } =
        new("privacy.export.ratelimit", SettingScope.Runtime, SettingDirection.AnyChange, 3);

    /// <summary>The run-up an account gets when its policy raises the assurance it demands.</summary>
    public static DurationSetting PolicyEnforcementGrace { get; } =
        new("policy.enforcement.grace", SettingScope.Runtime, SettingDirection.Increase, TimeSpan.Zero, ceiling: TimeSpan.FromDays(90));

    /// <summary>The shortest password accepted where no second step is held.</summary>
    public static IntegerSetting PasswordFloorSingleFactor { get; } =
        new("password.floor.singlefactor", SettingScope.Runtime, SettingDirection.AnyChange, 15, floor: 15);

    /// <summary>
    /// The shortest password accepted beside a second step, which may not exceed the
    /// single-factor floor.
    /// </summary>
    public static IntegerSetting PasswordFloorWithMfa { get; } =
        new("password.floor.withmfa", SettingScope.Runtime, SettingDirection.AnyChange, 10, floor: 8);

    /// <summary>The longest password accepted.</summary>
    public static IntegerSetting PasswordMaximum { get; } =
        new("password.maximum", SettingScope.Runtime, SettingDirection.AnyChange, 128, floor: 64);

    /// <summary>Where the leaked-password list comes from.</summary>
    public static ChoiceSetting<BlocklistSource> PasswordBlocklistSource { get; } =
        new("password.blocklist.source", SettingScope.Runtime, SettingDirection.AnyChange, BlocklistSource.RangeApi, Enum.GetValues<BlocklistSource>().ToFrozenSet());

    /// <summary>
    /// What a password is screened against. Adding a source is a tightening; the
    /// leaked list cannot be dropped.
    /// </summary>
    public static MultipleChoiceSetting<BlocklistRejectionSource> PasswordBlocklistSources { get; } =
        new(
            "password.blocklist.sources",
            SettingScope.Runtime,
            SettingDirection.Decrease,
            new[] { BlocklistRejectionSource.Leaked }.ToFrozenSet(),
            Enum.GetValues<BlocklistRejectionSource>().ToFrozenSet(),
            new[] { BlocklistRejectionSource.Leaked }.ToFrozenSet(),
            minimum: 1);

    /// <summary>How stale the leaked-password corpus may be.</summary>
    public static DurationSetting PasswordBlocklistCorpusMaxAge { get; } =
        new("password.blocklist.corpusmaxage", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(30));

    /// <summary>
    /// Argon2id memory in kibibytes. Its floor is the strength-class rule it shares
    /// with the iteration count, which <see cref="Argon2StrengthClasses"/> holds.
    /// </summary>
    public static IntegerSetting PasswordArgon2Memory { get; } =
        new("password.argon2.memory", SettingScope.Runtime, SettingDirection.AnyChange, 19456);

    /// <summary>
    /// Argon2id iterations. Its floor is the strength-class rule it shares with the
    /// memory.
    /// </summary>
    public static IntegerSetting PasswordArgon2Iterations { get; } =
        new("password.argon2.iterations", SettingScope.Runtime, SettingDirection.AnyChange, 2);

    /// <summary>Argon2id parallelism.</summary>
    public static IntegerSetting PasswordArgon2Parallelism { get; } =
        new("password.argon2.parallelism", SettingScope.Runtime, SettingDirection.AnyChange, 1);

    /// <summary>Steps either side of the current one a time-based code is accepted at.</summary>
    public static IntegerSetting FactorTotpDrift { get; } =
        new("factor.totp.drift", SettingScope.Runtime, SettingDirection.AnyChange, 1);

    /// <summary>Recovery codes issued in a set.</summary>
    public static IntegerSetting FactorRecoveryCodesCount { get; } =
        new("factor.recoverycodes.count", SettingScope.Runtime, SettingDirection.AnyChange, 10);

    /// <summary>
    /// How long a browser may skip the second step. Never offered where the policy
    /// requires AAL2.
    /// </summary>
    public static DurationSetting FactorTrustedDeviceLifetime { get; } =
        new("factor.trusteddevice.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(30), ceiling: TimeSpan.FromDays(90));

    /// <summary>Consecutive wrong passwords that revoke a browser's trust.</summary>
    public static IntegerSetting FactorTrustedDeviceFailureLimit { get; } =
        new("factor.trusteddevice.failurelimit", SettingScope.Runtime, SettingDirection.AnyChange, 3, ceiling: 5);

    /// <summary>
    /// Whether an account whose reachable assurance is AAL1 is sent a code before a
    /// sign-in from an unseen browser completes.
    /// </summary>
    public static FlagSetting DeviceVerificationEnabled { get; } =
        new("device.verification.enabled", SettingScope.Runtime, SettingDirection.Decrease, true);

    /// <summary>How long a browser that passed the new-device check is remembered.</summary>
    public static DurationSetting DeviceVerificationLifetime { get; } =
        new("device.verification.lifetime", SettingScope.Runtime, SettingDirection.Increase, TimeSpan.FromDays(90), ceiling: TimeSpan.FromDays(365));

    /// <summary>The age at which one reminder fires for a recovery-code set.</summary>
    public static DurationSetting RecoveryCodesReminder { get; } =
        new("recovery.codes.reminder", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(365));

    /// <summary>
    /// The relying party identifier. Empty is the chapter's "derived": it is taken
    /// from the configured origins at startup, which is also where it is checked to
    /// be a registrable suffix of one.
    /// </summary>
    public static TextSetting WebAuthnRelyingPartyId { get; } =
        new("webauthn.rpid", SettingScope.Protected, SettingDirection.AnyChange, string.Empty);

    /// <summary>The origins a registration is accepted from. The deployment names them.</summary>
    public static TextListSetting WebAuthnOrigins { get; } =
        new("webauthn.origins", SettingScope.Protected, SettingDirection.AnyChange, minimum: 1);

    /// <summary>Origins related to the relying party identifier.</summary>
    public static TextListSetting WebAuthnRelatedOrigins { get; } =
        new("webauthn.relatedorigins", SettingScope.Runtime, SettingDirection.AnyChange, []);

    /// <summary>
    /// The COSE algorithms a registration accepts, in order of preference. ES256
    /// cannot be dropped.
    /// </summary>
    public static IntegerListSetting WebAuthnAlgorithms { get; } =
        new("webauthn.algorithms", SettingScope.Protected, SettingDirection.AnyChange, [-8, -7, -257], new[] { -7 }.ToFrozenSet());

    /// <summary>Approvals an administrator-assisted recovery needs.</summary>
    public static IntegerSetting RecoveryApproversRequired { get; } =
        new("recovery.approvers.required", SettingScope.Runtime, SettingDirection.AnyChange, 1);

    /// <summary>How long a recovery link lives.</summary>
    public static DurationSetting RecoveryLinkLifetime { get; } =
        new("recovery.link.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromHours(1));

    /// <summary>From a loss report to the authenticator's invalidation.</summary>
    public static DurationSetting RecoveryInvalidationWindow { get; } =
        new("recovery.invalidation.window", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(7));

    /// <summary>
    /// How long a break-glass session lives, during which it satisfies every gate.
    /// </summary>
    public static DurationSetting BreakGlassSessionLifetime { get; } =
        new("breakglass.session.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromHours(4), ceiling: TimeSpan.FromHours(12));

    /// <summary>Whether progressive delay runs at all.</summary>
    public static FlagSetting AbuseThrottleEnabled { get; } =
        new("abuse.throttle.enabled", SettingScope.Protected, SettingDirection.Decrease, true);

    /// <summary>Consecutive failures before the first delay.</summary>
    public static IntegerSetting AbuseThrottleThreshold { get; } =
        new("abuse.throttle.threshold", SettingScope.Runtime, SettingDirection.AnyChange, 3);

    /// <summary>The first delay after the threshold.</summary>
    public static DurationSetting AbuseThrottleDelayInitial { get; } =
        new("abuse.throttle.delay.initial", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromSeconds(1));

    /// <summary>What the delay is multiplied by on each further failure.</summary>
    public static DecimalSetting AbuseThrottleDelayFactor { get; } =
        new("abuse.throttle.delay.factor", SettingScope.Runtime, SettingDirection.AnyChange, 2.0m, floor: 1.0m);

    /// <summary>The longest delay one source is held to.</summary>
    public static DurationSetting AbuseThrottleDelayMax { get; } =
        new("abuse.throttle.delay.max", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromSeconds(60), ceiling: TimeSpan.FromMinutes(10));

    /// <summary>
    /// The cap on the per-account component, which keeps the denial-of-service lever
    /// small.
    /// </summary>
    public static DurationSetting AbuseThrottleAccountCap { get; } =
        new("abuse.throttle.account.cap", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromSeconds(30), ceiling: TimeSpan.FromSeconds(60));

    /// <summary>The half-life of the accumulated delay while no failure occurs.</summary>
    public static DurationSetting AbuseThrottleDecay { get; } =
        new("abuse.throttle.decay", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromMinutes(10));

    /// <summary>
    /// One notice per address per window, whether the address is unknown or already
    /// held by someone.
    /// </summary>
    public static DurationSetting AbuseNonexistentWindow { get; } =
        new("abuse.nonexistent.window", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromHours(1));

    /// <summary>The prepaid balance below which sends are hard-stopped. The deployment names it.</summary>
    public static DecimalSetting AbuseSmsBalanceFloor { get; } =
        new("abuse.sms.balancefloor", SettingScope.Runtime, SettingDirection.AnyChange);

    /// <summary>The named restriction set governing every send.</summary>
    public static RestrictionSetSetting Restrictions { get; } =
        new("restrictions", SettingScope.Runtime, SettingDirection.AnyChange, ShippedRestrictions);

    /// <summary>How long a verification code lives.</summary>
    public static DurationSetting CodeVerificationLifetime { get; } =
        new("code.verification.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromMinutes(10), ceiling: TimeSpan.FromMinutes(30));

    /// <summary>
    /// Wrong tries after which a verification code is invalidated and a correct one
    /// refused.
    /// </summary>
    public static IntegerSetting CodeVerificationAttempts { get; } =
        new("code.verification.attempts", SettingScope.Runtime, SettingDirection.AnyChange, 5, ceiling: 10);

    /// <summary>How long a sign-in link lives.</summary>
    public static DurationSetting LinkMagicLifetime { get; } =
        new("link.magic.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromMinutes(15), ceiling: TimeSpan.FromHours(1));

    /// <summary>How long a single-use invitation link lives.</summary>
    public static DurationSetting LinkInvitationLifetime { get; } =
        new("link.invitation.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(7), ceiling: TimeSpan.FromDays(30));

    /// <summary>
    /// The longest side in pixels a profile photo is kept at; a larger image is
    /// downscaled.
    /// </summary>
    public static IntegerSetting PhotoMaxDimension { get; } =
        new("photo.maxdimension", SettingScope.Runtime, SettingDirection.AnyChange, 1024);

    /// <summary>Whether an unusual read volume per actor raises an alert.</summary>
    public static FlagSetting ExfiltrationReadVolumeAlerting { get; } =
        new("exfiltration.readvolume.alerting", SettingScope.Runtime, SettingDirection.AnyChange, true);

    /// <summary>
    /// Whether a staff bulk export needs step-up. A subject's own export is gated at
    /// the account's reachable assurance instead.
    /// </summary>
    public static FlagSetting ExfiltrationExportStepUpRequired { get; } =
        new("exfiltration.export.stepuprequired", SettingScope.Runtime, SettingDirection.AnyChange, true);

    /// <summary>Staff bulk exports admitted in an hour.</summary>
    public static IntegerSetting ExfiltrationExportRateLimit { get; } =
        new("exfiltration.export.ratelimit", SettingScope.Runtime, SettingDirection.AnyChange, 5);

    /// <summary>Whether every export is recorded.</summary>
    public static FlagSetting ExfiltrationExportAuditing { get; } =
        new("exfiltration.export.auditing", SettingScope.Protected, SettingDirection.Decrease, true);

    /// <summary>Where alerts are emailed. The deployment names at least one.</summary>
    public static TextListSetting AlertingEmailDestinations { get; } =
        new("alerting.email.destinations", SettingScope.Runtime, SettingDirection.AnyChange, minimum: 1);

    /// <summary>Where high-severity alerts are texted. The deployment names at least one.</summary>
    public static TextListSetting AlertingSmsDestinations { get; } =
        new("alerting.sms.destinations", SettingScope.Runtime, SettingDirection.AnyChange, minimum: 1);

    /// <summary>
    /// Whether routine alerts reach the owner. Break-glass events reach them either
    /// way.
    /// </summary>
    public static FlagSetting AlertingOwnerEnabled { get; } =
        new("alerting.owner.enabled", SettingScope.Runtime, SettingDirection.AnyChange, false);

    /// <summary>The owner's email destination. The deployment names it.</summary>
    public static TextSetting AlertingOwnerEmail { get; } =
        new("alerting.owner.email", SettingScope.Runtime, SettingDirection.AnyChange);

    /// <summary>The owner's SMS destination. The deployment names it.</summary>
    public static TextSetting AlertingOwnerSms { get; } =
        new("alerting.owner.sms", SettingScope.Runtime, SettingDirection.AnyChange);

    /// <summary>
    /// Whether a change of alert destination is delivered to the previous
    /// destinations.
    /// </summary>
    public static FlagSetting AlertingDestinationChangeNotify { get; } =
        new("alerting.destinationchange.notify", SettingScope.Protected, SettingDirection.Decrease, true);

    /// <summary>The window a read-volume baseline is drawn from.</summary>
    public static DurationSetting ExfiltrationReadVolumeBaselineWindow { get; } =
        new("exfiltration.readvolume.baselinewindow", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(30));

    /// <summary>How long one condition is reported once.</summary>
    public static DurationSetting AlertingDedupeWindow { get; } =
        new("alerting.dedupe.window", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromHours(1));

    /// <summary>The severity at which an alert is also texted.</summary>
    public static ChoiceSetting<AlertSeverity> AlertingSmsSeverityThreshold { get; } =
        new("alerting.sms.severitythreshold", SettingScope.Runtime, SettingDirection.AnyChange, AlertSeverity.High, Enum.GetValues<AlertSeverity>().ToFrozenSet());

    /// <summary>How far ahead an expiry on the maintenance log is warned about.</summary>
    public static DurationSetting MaintenanceExpiryWarningLead { get; } =
        new("maintenance.expiry.warninglead", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(30));

    /// <summary>
    /// The measurable bound on a reverse lookup, which is the authorization seam's
    /// migration trigger.
    /// </summary>
    public static DurationSetting AuthzReverseLookupBudget { get; } =
        new("authz.reverselookup.budget", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromSeconds(2));

    /// <summary>How long an organization's deletion stays cancellable.</summary>
    public static DurationSetting OrganizationDeletionGrace { get; } =
        new("organization.deletion.grace", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(30));

    /// <summary>From a takedown to its erasure, during which it can be reversed.</summary>
    public static DurationSetting TakedownGrace { get; } =
        new("takedown.grace", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(7));

    /// <summary>How long an account's deletion stays cancellable.</summary>
    public static DurationSetting AccountDeletionGrace { get; } =
        new("account.deletion.grace", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(30));

    /// <summary>Whether one account may hold memberships in several organizations.</summary>
    public static FlagSetting OrganizationMultipleMemberships { get; } =
        new("organization.multiplememberships", SettingScope.Runtime, SettingDirection.AnyChange, false);

    /// <summary>The undo window after an identifier is removed or replaced.</summary>
    public static DurationSetting IdentifierChangeCoolingOff { get; } =
        new("identifier.change.coolingoff", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromHours(72));

    /// <summary>
    /// Whether an account has to hold a verified phone. Phone is never the sole
    /// identifier.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> RegistrationPhone { get; } =
        new(
            "registration.phone",
            SettingScope.Runtime,
            SettingDirection.Decrease,
            AttributeRequirement.Required,
            new[] { AttributeRequirement.Required, AttributeRequirement.Optional }.ToFrozenSet());

    /// <summary>
    /// Whether an under-age date ends the registration session, or only records the
    /// age group.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> RegistrationAdultAffirmation { get; } =
        new(
            "registration.adultaffirmation",
            SettingScope.Runtime,
            SettingDirection.AnyChange,
            AttributeRequirement.Required,
            new[] { AttributeRequirement.Required, AttributeRequirement.Off }.ToFrozenSet());

    /// <summary>
    /// How long a registration session lives before it is swept, leaving nothing.
    /// </summary>
    public static DurationSetting RegistrationSessionLifetime { get; } =
        new("registration.session.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromHours(24), ceiling: TimeSpan.FromHours(72));

    /// <summary>
    /// Whether usernames exist. While off, no request accepts one and no response
    /// carries the field.
    /// </summary>
    public static FlagSetting IdentifiersUsernameEnabled { get; } =
        new("identifiers.username.enabled", SettingScope.Runtime, SettingDirection.AnyChange, false);

    /// <summary>The shortest interval between username changes.</summary>
    public static DurationSetting IdentifiersUsernameChangeCoolOff { get; } =
        new("identifiers.username.changecooloff", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(30));

    /// <summary>
    /// Whether a legal name is collected, which is a proofing attribute collected
    /// only with a declared purpose.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> ProfileLegalName { get; } =
        new("profile.legalname", SettingScope.Runtime, SettingDirection.AnyChange, AttributeRequirement.Off, Enum.GetValues<AttributeRequirement>().ToFrozenSet());

    /// <summary>
    /// Whether the date entered at the age step is retained. The date is immutable to
    /// the person.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> ProfileDateOfBirth { get; } =
        new("profile.dateofbirth", SettingScope.Runtime, SettingDirection.AnyChange, AttributeRequirement.Off, Enum.GetValues<AttributeRequirement>().ToFrozenSet());

    /// <summary>
    /// Working days from a privacy request's submission to its decision. A lapse is a
    /// deemed rejection.
    /// </summary>
    public static IntegerSetting PrivacyRequestDecision { get; } =
        new("privacy.request.decision", SettingScope.Runtime, SettingDirection.AnyChange, 6);

    /// <summary>The week on which working days are counted.</summary>
    public static MultipleChoiceSetting<DayOfWeek> PrivacyWorkingDays { get; } =
        new(
            "privacy.workingdays",
            SettingScope.Runtime,
            SettingDirection.AnyChange,
            new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday }.ToFrozenSet(),
            Enum.GetValues<DayOfWeek>().ToFrozenSet(),
            FrozenSet<DayOfWeek>.Empty,
            minimum: 1);

    /// <summary>
    /// Public holidays, added and moved as they are announced. An unlisted holiday
    /// makes a deadline earlier, which is always compliant, so the safe default is
    /// empty and every change is a loosening.
    /// </summary>
    public static DateListSetting PrivacyHolidays { get; } =
        new("privacy.holidays", SettingScope.Runtime, SettingDirection.AnyChange);

    /// <summary>
    /// Working days before a decision deadline at which it is warned about.
    /// </summary>
    public static IntegerSetting PrivacyRequestWarningLead { get; } =
        new("privacy.request.warninglead", SettingScope.Runtime, SettingDirection.AnyChange, 2);

    /// <summary>
    /// How long security events, permission changes and financial actions are kept.
    /// </summary>
    public static DurationSetting RetentionAuditSecurity { get; } =
        new("retention.audit.security", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(365 * 7), floor: TimeSpan.FromDays(365 * 5));

    /// <summary>How long routine access logging is kept.</summary>
    public static DurationSetting RetentionAuditRoutine { get; } =
        new("retention.audit.routine", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(90), floor: TimeSpan.FromDays(30));

    /// <summary>
    /// How long a consent record is kept after the processing it covered ends.
    /// </summary>
    public static DurationSetting RetentionConsent { get; } =
        new("retention.consent", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(365 * 3), floor: TimeSpan.FromDays(365));

    /// <summary>
    /// How long a backup is kept, which also bounds how long a pre-erasure backup
    /// survives.
    /// </summary>
    public static DurationSetting BackupRetention { get; } =
        new("backup.retention", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(35), floor: TimeSpan.FromDays(14));

    /// <summary>
    /// Whether the deployment is hosted inside or outside Egypt. The deployment names
    /// it, and an outside value makes the cross-border basis required.
    /// </summary>
    public static ChoiceSetting<Janus.Core.Configuration.HostingLocation> HostingLocation { get; } =
        new("hosting.location", SettingScope.Protected, SettingDirection.AnyChange, Enum.GetValues<Janus.Core.Configuration.HostingLocation>().ToFrozenSet());

    /// <summary>
    /// The basis for hosting outside Egypt, which the deployment names when it hosts
    /// there.
    /// </summary>
    public static TextSetting HostingCrossBorderBasis { get; } =
        new("hosting.crossborderbasis", SettingScope.Protected, SettingDirection.AnyChange);

    /// <summary>
    /// The default governing language of every legal document version. The deployment
    /// names it.
    /// </summary>
    public static TextSetting LegalGoverningLanguage { get; } =
        new("legal.governinglanguage", SettingScope.Protected, SettingDirection.AnyChange);

    /// <summary>Whether the audit log is written.</summary>
    public static FlagSetting AuditEnabled { get; } =
        new("audit.enabled", SettingScope.Protected, SettingDirection.Decrease, true);

    /// <summary>Whether a token's signature is verified.</summary>
    public static FlagSetting TokenSignatureVerification { get; } =
        new("token.signature.verification", SettingScope.Protected, SettingDirection.Decrease, true);

    /// <summary>
    /// How long an access token lives, which for a relying party that validates
    /// offline is the revocation latency.
    /// </summary>
    public static DurationSetting OidcAccessTokenLifetime { get; } =
        new("oidc.accesstoken.lifetime", SettingScope.Runtime, SettingDirection.Increase, TimeSpan.FromMinutes(10), ceiling: TimeSpan.FromHours(1));

    /// <summary>How long an authorization code lives.</summary>
    public static DurationSetting OidcCodeLifetime { get; } =
        new("oidc.code.lifetime", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromSeconds(60), ceiling: TimeSpan.FromMinutes(10));

    /// <summary>
    /// The algorithm tokens are signed with. The one place the value is held.
    /// </summary>
    public static ChoiceSetting<string> TokenSigningAlgorithm { get; } =
        new("token.signing.algorithm", SettingScope.Protected, SettingDirection.AnyChange, "ES256", new[] { "ES256" }.ToFrozenSet(StringComparer.Ordinal));

    /// <summary>
    /// How often the signing key is rotated. The overlap is the access-token lifetime
    /// plus five minutes, and is not a key of its own.
    /// </summary>
    public static DurationSetting TokenSigningRotation { get; } =
        new("token.signing.rotation", SettingScope.Runtime, SettingDirection.AnyChange, TimeSpan.FromDays(90));

    /// <summary>
    /// What an organization changes about the system policy: one key per organization,
    /// created with no override when the organization is.
    /// </summary>
    public static SettingFamily<PolicyOverride> OrganizationPolicy { get; } =
        new("policy", SettingScope.Runtime, SettingDirection.AnyChange, PolicyOverride.None);

    /// <summary>
    /// How long a host-declared category of data is kept: one key per declared
    /// category, whose floor the host declares. Startup fails for a declared category
    /// without one.
    /// </summary>
    public static SettingFamily<TimeSpan> HostCategoryRetention { get; } =
        new("retention", SettingScope.Runtime, SettingDirection.AnyChange);

    /// <summary>
    /// Whether step-up is enforced for an organization: one key per organization, and
    /// the protected kill switch rather than a field of the policy object.
    /// </summary>
    public static SettingFamily<bool> OrganizationStepUpEnforcement { get; } =
        new("stepup.enforcement", SettingScope.Protected, SettingDirection.Decrease, true);

    /// <summary>
    /// The (memory, iterations) pairs the Argon2id floor admits. A deployment is at or
    /// above the floor when its pair is at or above one of these.
    /// </summary>
    public static IReadOnlyList<Argon2StrengthClass> Argon2StrengthClasses { get; } =
    [
        new(19456, 2),
        new(12288, 3),
        new(9216, 4),
        new(7168, 5),
    ];

    /// <summary>
    /// Every key of chapter 10 section 4 that exists once for the deployment.
    /// </summary>
    public static IReadOnlyList<Setting> All { get; } =
    [
        SessionAal2Inactivity,
        SessionAal2Absolute,
        SessionDefaultInactivity,
        SessionDefaultAbsolute,
        SessionStepUpRecency,
        PolicyDefault,
        PrivacyExportRateLimit,
        PolicyEnforcementGrace,
        PasswordFloorSingleFactor,
        PasswordFloorWithMfa,
        PasswordMaximum,
        PasswordBlocklistSource,
        PasswordBlocklistSources,
        PasswordBlocklistCorpusMaxAge,
        PasswordArgon2Memory,
        PasswordArgon2Iterations,
        PasswordArgon2Parallelism,
        FactorTotpDrift,
        FactorRecoveryCodesCount,
        FactorTrustedDeviceLifetime,
        FactorTrustedDeviceFailureLimit,
        DeviceVerificationEnabled,
        DeviceVerificationLifetime,
        RecoveryCodesReminder,
        WebAuthnRelyingPartyId,
        WebAuthnOrigins,
        WebAuthnRelatedOrigins,
        WebAuthnAlgorithms,
        RecoveryApproversRequired,
        RecoveryLinkLifetime,
        RecoveryInvalidationWindow,
        BreakGlassSessionLifetime,
        AbuseThrottleEnabled,
        AbuseThrottleThreshold,
        AbuseThrottleDelayInitial,
        AbuseThrottleDelayFactor,
        AbuseThrottleDelayMax,
        AbuseThrottleAccountCap,
        AbuseThrottleDecay,
        AbuseNonexistentWindow,
        AbuseSmsBalanceFloor,
        Restrictions,
        CodeVerificationLifetime,
        CodeVerificationAttempts,
        LinkMagicLifetime,
        LinkInvitationLifetime,
        PhotoMaxDimension,
        ExfiltrationReadVolumeAlerting,
        ExfiltrationExportStepUpRequired,
        ExfiltrationExportRateLimit,
        ExfiltrationExportAuditing,
        AlertingEmailDestinations,
        AlertingSmsDestinations,
        AlertingOwnerEnabled,
        AlertingOwnerEmail,
        AlertingOwnerSms,
        AlertingDestinationChangeNotify,
        ExfiltrationReadVolumeBaselineWindow,
        AlertingDedupeWindow,
        AlertingSmsSeverityThreshold,
        MaintenanceExpiryWarningLead,
        AuthzReverseLookupBudget,
        OrganizationDeletionGrace,
        TakedownGrace,
        AccountDeletionGrace,
        OrganizationMultipleMemberships,
        IdentifierChangeCoolingOff,
        RegistrationPhone,
        RegistrationAdultAffirmation,
        RegistrationSessionLifetime,
        IdentifiersUsernameEnabled,
        IdentifiersUsernameChangeCoolOff,
        ProfileLegalName,
        ProfileDateOfBirth,
        PrivacyRequestDecision,
        PrivacyWorkingDays,
        PrivacyHolidays,
        PrivacyRequestWarningLead,
        RetentionAuditSecurity,
        RetentionAuditRoutine,
        RetentionConsent,
        BackupRetention,
        HostingLocation,
        HostingCrossBorderBasis,
        LegalGoverningLanguage,
        AuditEnabled,
        TokenSignatureVerification,
        OidcAccessTokenLifetime,
        OidcCodeLifetime,
        TokenSigningAlgorithm,
        TokenSigningRotation,
    ];

    /// <summary>
    /// Every key of chapter 10 section 4 that exists once per organization or once
    /// per host-declared category.
    /// </summary>
    public static IReadOnlyList<SettingFamily> Families { get; } =
        [OrganizationPolicy, HostCategoryRetention, OrganizationStepUpEnforcement];

    /// <summary>
    /// The keys a deployment has to name, because they name the deployment and the
    /// library cannot guess them.
    /// </summary>
    public static IReadOnlyList<Setting> Required { get; } =
        [.. All.Where(setting => setting.IsRequired)];

    /// <summary>
    /// Checks the two password floors together. Each key carries its own floor; this
    /// is the rule between them, that the length required when a second factor is
    /// present never exceeds the length required without one.
    /// </summary>
    /// <param name="singleFactor">The value named for <c>password.floor.singlefactor</c>.</param>
    /// <param name="withMfa">The value named for <c>password.floor.withmfa</c>.</param>
    /// <returns>Success, or the failure naming the key and the bound it crossed.</returns>
    /// <remarks>Implements chapter 10 section 4.2, AUTH-PASS-001, D-140, OPS-CFG-003.</remarks>
    public static Result AcceptPasswordFloorPair(int singleFactor, int withMfa) =>
        withMfa > singleFactor
            ? Refuse(
                ErrorCodes.ConfigurationValueAboveCeiling,
                PasswordFloorWithMfa.Key,
                "ceiling",
                singleFactor.ToString(CultureInfo.InvariantCulture))
            : Result.Success();

    /// <summary>
    /// Checks the Argon2id memory and iterations together. Neither key has a floor of
    /// its own: the floor is that the pair is at or above one of the strength classes,
    /// which are of equal strength to each other.
    /// </summary>
    /// <param name="memory">The value named for <c>password.argon2.memory</c>, in kibibytes.</param>
    /// <param name="iterations">The value named for <c>password.argon2.iterations</c>.</param>
    /// <returns>Success, or the failure naming the classes the pair falls under.</returns>
    /// <remarks>Implements chapter 10 section 4.2, AUTH-PASS-007, D-120, D-135, OPS-CFG-003.</remarks>
    public static Result AcceptArgon2Cost(int memory, int iterations) =>
        Argon2StrengthClasses.Any(
            strength => memory >= strength.Memory && iterations >= strength.Iterations)
            ? Result.Success()
            : Refuse(
                ErrorCodes.ConfigurationValueBelowFloor,
                PasswordArgon2Memory.Key,
                "floor",
                string.Join(
                    ", ",
                    Argon2StrengthClasses.Select(strength => string.Create(
                        CultureInfo.InvariantCulture,
                        $"({strength.Memory}, {strength.Iterations})"))));

    /// <summary>
    /// Checks that a deployment named every key it has to name, before the library
    /// starts rather than at the first request that needs one.
    /// </summary>
    /// <param name="named">The keys the deployment named a value for.</param>
    /// <param name="location">
    /// Where the deployment holds its data, or <see langword="null"/> where it named
    /// no value for <c>hosting.location</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">The set of named keys is absent.</exception>
    /// <exception cref="StartupException">
    /// A key the deployment has to name carries no value. The exception names the key,
    /// and carries a code where chapter 10 section 1.5 gives the condition one.
    /// </exception>
    /// <remarks>
    /// Implements LIB-HOST-001, INT-HOST-001, CONV-ERR-001. Every other key resolves
    /// with its default, so a deployment that names these and nothing else starts.
    /// </remarks>
    public static void ThrowIfIncomplete(
        IReadOnlySet<ConfigurationKey> named,
        Janus.Core.Configuration.HostingLocation? location)
    {
        ArgumentNullException.ThrowIfNull(named);

        if (!named.Contains(LegalGoverningLanguage.Key))
        {
            throw Unnamed(LegalGoverningLanguage.Key, ErrorCodes.StartupGoverningLanguage);
        }

        foreach (Setting setting in Required)
        {
            // The cross-border basis is the one conditional declaration: a deployment
            // holding its data inside Egypt has no transfer to state (INT-HOST-001).
            if (setting.Key == HostingCrossBorderBasis.Key
                && location != Janus.Core.Configuration.HostingLocation.Outside)
            {
                continue;
            }

            if (!named.Contains(setting.Key))
            {
                throw Unnamed(setting.Key, code: null);
            }
        }
    }

    // The fault a missing declaration raises. Chapter 10 section 1.5 names a code for
    // the governing language and for no other declaration, so the rest carry the key
    // alone.
    private static StartupException Unnamed(ConfigurationKey key, ErrorCode? code)
    {
        string message = "The deployment names " + key + "; no value was supplied.";

        return code is { } named
            ? new StartupException(
                message,
                new Error(
                    named,
                    new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                    {
                        ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
                    }))
            : new StartupException(message);
    }

    // A failure of a rule that holds over two keys rather than one. The failure names
    // the key chapter 10 section 4 states the rule on, so the management application
    // shows it against the row the operator is editing.
    private static Result Refuse(ErrorCode code, ConfigurationKey key, string constraint, string expected) =>
        Result.Failure(new Error(
            code,
            new Dictionary<string, JsonElement>(capacity: 2, StringComparer.Ordinal)
            {
                ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
                [constraint] = JsonSerializer.SerializeToElement(expected),
            }));
}
