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
/// OPS-CFG-003, OPS-CFG-004. A key whose row names a loosening direction carries it;
/// every other key takes the direction of the section 4 preamble, read from its
/// bounds (OPS-CFG-002, D-152).
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
        new("session.aal2.inactivity", SettingScope.Runtime, "PT1H", ceiling: "PT12H");

    /// <summary>How long a session may live where the policy requires AAL2.</summary>
    public static DurationSetting SessionAal2Absolute { get; } =
        new("session.aal2.absolute", SettingScope.Runtime, "PT24H", ceiling: "PT24H");

    /// <summary>How long a session under the system policy may sit idle, refreshed by use.</summary>
    public static DurationSetting SessionDefaultInactivity { get; } =
        new("session.default.inactivity", SettingScope.Runtime, "P90D", ceiling: "P365D");

    /// <summary>The definite overall timeout of a session under the system policy.</summary>
    public static DurationSetting SessionDefaultAbsolute { get; } =
        new("session.default.absolute", SettingScope.Runtime, "P365D", ceiling: "P365D");

    /// <summary>How recently the factors that satisfy a gate have to have been presented.</summary>
    public static DurationSetting SessionStepUpRecency { get; } =
        new("session.stepup.recency", SettingScope.Runtime, "PT15M");

    /// <summary>The system policy, for principals with no membership.</summary>
    public static PolicySetting PolicyDefault { get; } =
        new("policy.default", SettingScope.Runtime, Policies.SystemDefault);

    /// <summary>Subject exports one account may ask for in a day.</summary>
    public static IntegerSetting PrivacyExportRateLimit { get; } =
        new("privacy.export.ratelimit", SettingScope.Runtime, 3);

    /// <summary>The run-up an account gets when its policy raises the assurance it demands.</summary>
    public static DurationSetting PolicyEnforcementGrace { get; } =
        new(
            "policy.enforcement.grace",
            SettingScope.Runtime,
            "PT0S",
            ceiling: "P90D",
            loosening: SettingDirection.Increase);

    /// <summary>The shortest password accepted where no second step is held.</summary>
    public static IntegerSetting PasswordFloorSingleFactor { get; } =
        new("password.floor.singlefactor", SettingScope.Runtime, 15, floor: 15);

    /// <summary>
    /// The shortest password accepted beside a second step, which may not exceed the
    /// single-factor floor.
    /// </summary>
    public static IntegerSetting PasswordFloorWithMfa { get; } =
        new("password.floor.withmfa", SettingScope.Runtime, 10, floor: 8);

    /// <summary>The longest password accepted.</summary>
    public static IntegerSetting PasswordMaximum { get; } =
        new("password.maximum", SettingScope.Runtime, 128, floor: 64);

    /// <summary>Where the leaked-password list comes from.</summary>
    public static ChoiceSetting<BlocklistSource> PasswordBlocklistSource { get; } =
        new(
            "password.blocklist.source",
            SettingScope.Runtime,
            BlocklistSource.RangeApi,
            Enum.GetValues<BlocklistSource>().ToFrozenSet());

    /// <summary>
    /// What a password is screened against. Adding a source is a tightening; the
    /// leaked list cannot be dropped.
    /// </summary>
    public static MultipleChoiceSetting<BlocklistRejectionSource> PasswordBlocklistSources { get; } =
        new(
            "password.blocklist.sources",
            SettingScope.Runtime,
            new[] { BlocklistRejectionSource.Leaked }.ToFrozenSet(),
            Enum.GetValues<BlocklistRejectionSource>().ToFrozenSet(),
            new[] { BlocklistRejectionSource.Leaked }.ToFrozenSet(),
            minimum: 1,
            loosening: SettingDirection.Decrease);

    /// <summary>
    /// Where the corpus a deployment hosts itself answers. It serves the same ranges
    /// the primary source does, which is the whole of bringing the integration
    /// in-house. The deployment names it where <c>password.blocklist.source</c> is
    /// <c>selfHosted</c>.
    /// </summary>
    public static TextSetting PasswordBlocklistSelfHostedAddress { get; } =
        new("password.blocklist.selfhosted.address", SettingScope.Runtime);

    /// <summary>How stale the leaked-password corpus may be.</summary>
    public static DurationSetting PasswordBlocklistCorpusMaxAge { get; } =
        new("password.blocklist.corpusmaxage", SettingScope.Runtime, "P30D");

    /// <summary>
    /// Argon2id memory in kibibytes. Its floor is the strength-class rule it shares
    /// with the iteration count, which <see cref="Argon2StrengthClasses"/> holds.
    /// </summary>
    public static IntegerSetting PasswordArgon2Memory { get; } =
        new("password.argon2.memory", SettingScope.Runtime, 19456);

    /// <summary>
    /// Argon2id iterations. Its floor is the strength-class rule it shares with the
    /// memory.
    /// </summary>
    public static IntegerSetting PasswordArgon2Iterations { get; } =
        new("password.argon2.iterations", SettingScope.Runtime, 2);

    /// <summary>Argon2id parallelism.</summary>
    public static IntegerSetting PasswordArgon2Parallelism { get; } =
        new("password.argon2.parallelism", SettingScope.Runtime, 1);

    /// <summary>Steps either side of the current one a time-based code is accepted at.</summary>
    public static IntegerSetting FactorTotpDrift { get; } =
        new("factor.totp.drift", SettingScope.Runtime, 1);

    /// <summary>Recovery codes issued in a set.</summary>
    public static IntegerSetting FactorRecoveryCodesCount { get; } =
        new("factor.recoverycodes.count", SettingScope.Runtime, 10);

    /// <summary>
    /// How long a browser may skip the second step. Never offered where the policy
    /// requires AAL2.
    /// </summary>
    public static DurationSetting FactorTrustedDeviceLifetime { get; } =
        new("factor.trusteddevice.lifetime", SettingScope.Runtime, "P30D", ceiling: "P90D");

    /// <summary>Consecutive wrong passwords that revoke a browser's trust.</summary>
    public static IntegerSetting FactorTrustedDeviceFailureLimit { get; } =
        new("factor.trusteddevice.failurelimit", SettingScope.Runtime, 3, ceiling: 5);

    /// <summary>
    /// Whether an account whose reachable assurance is AAL1 is sent a code before a
    /// sign-in from an unseen browser completes.
    /// </summary>
    public static FlagSetting DeviceVerificationEnabled { get; } =
        new("device.verification.enabled", SettingScope.Runtime, true);

    /// <summary>How long a browser that passed the new-device check is remembered.</summary>
    public static DurationSetting DeviceVerificationLifetime { get; } =
        new(
            "device.verification.lifetime",
            SettingScope.Runtime,
            "P90D",
            ceiling: "P365D",
            loosening: SettingDirection.Increase);

    /// <summary>The age at which one reminder fires for a recovery-code set.</summary>
    public static DurationSetting RecoveryCodesReminder { get; } =
        new("recovery.codes.reminder", SettingScope.Runtime, "P365D");

    /// <summary>
    /// The relying party identifier. Empty is the chapter's "derived": it is taken
    /// from the configured origins at startup, which is also where it is checked to
    /// be a registrable suffix of one.
    /// </summary>
    public static TextSetting WebAuthnRelyingPartyId { get; } =
        new("webauthn.rpid", SettingScope.Protected, string.Empty);

    /// <summary>The origins a registration is accepted from. The deployment names them.</summary>
    public static TextListSetting WebAuthnOrigins { get; } =
        new("webauthn.origins", SettingScope.Protected, minimum: 1);

    /// <summary>Origins related to the relying party identifier.</summary>
    public static TextListSetting WebAuthnRelatedOrigins { get; } =
        new("webauthn.relatedorigins", SettingScope.Runtime, []);

    /// <summary>
    /// The COSE algorithms a registration accepts, in order of preference. ES256
    /// cannot be dropped.
    /// </summary>
    public static IntegerListSetting WebAuthnAlgorithms { get; } =
        new("webauthn.algorithms", SettingScope.Protected, [-8, -7, -257], new[] { -7 }.ToFrozenSet());

    /// <summary>Approvals an administrator-assisted recovery needs.</summary>
    public static IntegerSetting RecoveryApproversRequired { get; } =
        new("recovery.approvers.required", SettingScope.Runtime, 1);

    /// <summary>How long a recovery link lives.</summary>
    public static DurationSetting RecoveryLinkLifetime { get; } =
        new("recovery.link.lifetime", SettingScope.Runtime, "PT1H");

    /// <summary>From a loss report to the authenticator's invalidation.</summary>
    public static DurationSetting RecoveryInvalidationWindow { get; } =
        new("recovery.invalidation.window", SettingScope.Runtime, "P7D");

    /// <summary>
    /// How often the loss-report notice repeats across the invalidation window,
    /// beside the one at the report and the one a day before invalidation.
    /// </summary>
    public static DurationSetting RecoveryInvalidationNoticeInterval { get; } =
        new("recovery.invalidation.noticeinterval", SettingScope.Runtime, "P1D");

    /// <summary>Recovery requests accepted for one account in a day.</summary>
    public static IntegerSetting RecoveryRateLimitAccount { get; } =
        new("recovery.ratelimit.account", SettingScope.Runtime, 3, loosening: SettingDirection.Increase);

    /// <summary>Approvals one approver may give in a day.</summary>
    public static IntegerSetting RecoveryRateLimitApprover { get; } =
        new("recovery.ratelimit.approver", SettingScope.Runtime, 5, loosening: SettingDirection.Increase);

    /// <summary>
    /// How long a break-glass session lives, during which it satisfies every gate.
    /// </summary>
    public static DurationSetting BreakGlassSessionLifetime { get; } =
        new("breakglass.session.lifetime", SettingScope.Runtime, "PT4H", ceiling: "PT12H");

    /// <summary>Whether progressive delay runs at all.</summary>
    public static FlagSetting AbuseThrottleEnabled { get; } =
        new("abuse.throttle.enabled", SettingScope.Protected, true);

    /// <summary>Consecutive failures before the first delay.</summary>
    public static IntegerSetting AbuseThrottleThreshold { get; } =
        new("abuse.throttle.threshold", SettingScope.Runtime, 3);

    /// <summary>The first delay after the threshold.</summary>
    public static DurationSetting AbuseThrottleDelayInitial { get; } =
        new("abuse.throttle.delay.initial", SettingScope.Runtime, "PT1S");

    /// <summary>What the delay is multiplied by on each further failure.</summary>
    public static DecimalSetting AbuseThrottleDelayFactor { get; } =
        new("abuse.throttle.delay.factor", SettingScope.Runtime, 2.0m, floor: 1.0m);

    /// <summary>The longest delay one source is held to.</summary>
    public static DurationSetting AbuseThrottleDelayMax { get; } =
        new("abuse.throttle.delay.max", SettingScope.Runtime, "PT60S", ceiling: "PT10M");

    /// <summary>
    /// The cap on the per-account component, which keeps the denial-of-service lever
    /// small.
    /// </summary>
    public static DurationSetting AbuseThrottleAccountCap { get; } =
        new("abuse.throttle.account.cap", SettingScope.Runtime, "PT30S", ceiling: "PT60S");

    /// <summary>The half-life of the accumulated delay while no failure occurs.</summary>
    public static DurationSetting AbuseThrottleDecay { get; } =
        new("abuse.throttle.decay", SettingScope.Runtime, "PT10M");

    /// <summary>
    /// One notice per address per window, whether the address is unknown or already
    /// held by someone.
    /// </summary>
    public static DurationSetting AbuseNonexistentWindow { get; } =
        new("abuse.nonexistent.window", SettingScope.Runtime, "PT1H");

    /// <summary>
    /// Requests admitted from one source address a minute, before any expensive work.
    /// </summary>
    public static IntegerSetting AbuseSourceRateLimit { get; } =
        new("abuse.source.ratelimit", SettingScope.Runtime, 300, loosening: SettingDirection.Increase);

    /// <summary>
    /// Registration sessions from one source in an hour above which the repeated
    /// attempts signal fires.
    /// </summary>
    public static IntegerSetting AbuseBotDefenceRepeatedAttempts { get; } =
        new("abuse.botdefence.repeatedattempts", SettingScope.Runtime, 3, loosening: SettingDirection.Increase);

    /// <summary>
    /// Where the shipped default mail transport is called. Empty while the deployment
    /// supplies a transport of its own, which is called wherever it decides
    /// (LIB-EXT-001, INT-MAIL-008).
    /// </summary>
    public static TextSetting IntegrationMailEndpoint { get; } =
        new("integration.mail.endpoint", SettingScope.Protected, string.Empty);

    /// <summary>
    /// Where the shipped default SMS transport is called. Empty while the deployment
    /// supplies a transport of its own (LIB-EXT-001, INT-SMS-001).
    /// </summary>
    public static TextSetting IntegrationSmsEndpoint { get; } =
        new("integration.sms.endpoint", SettingScope.Protected, string.Empty);

    /// <summary>Callbacks accepted from one source a minute, before any lookup.</summary>
    public static IntegerSetting IntegrationCallbackRateLimit { get; } =
        new("integration.callback.ratelimit", SettingScope.Runtime, 60, loosening: SettingDirection.Increase);

    /// <summary>The named restriction set governing every send.</summary>
    public static RestrictionSetSetting Restrictions { get; } =
        new("restrictions", SettingScope.Runtime, ShippedRestrictions);

    /// <summary>How long a verification code lives.</summary>
    public static DurationSetting CodeVerificationLifetime { get; } =
        new("code.verification.lifetime", SettingScope.Runtime, "PT10M", ceiling: "PT30M");

    /// <summary>
    /// Wrong tries after which a verification code is invalidated and a correct one
    /// refused.
    /// </summary>
    public static IntegerSetting CodeVerificationAttempts { get; } =
        new("code.verification.attempts", SettingScope.Runtime, 5, ceiling: 10);

    /// <summary>How long a sign-in link lives.</summary>
    public static DurationSetting LinkMagicLifetime { get; } =
        new("link.magic.lifetime", SettingScope.Runtime, "PT15M", ceiling: "PT1H");

    /// <summary>How long a single-use invitation link lives.</summary>
    public static DurationSetting LinkInvitationLifetime { get; } =
        new("link.invitation.lifetime", SettingScope.Runtime, "P7D", ceiling: "P30D");

    /// <summary>The largest profile photo accepted, in bytes.</summary>
    public static IntegerSetting PhotoMaxBytes { get; } =
        new("photo.maxbytes", SettingScope.Runtime, 2097152, ceiling: 10485760);

    /// <summary>
    /// The longest side in pixels a profile photo is kept at; a larger image is
    /// downscaled.
    /// </summary>
    public static IntegerSetting PhotoMaxDimension { get; } =
        new("photo.maxdimension", SettingScope.Runtime, 1024);

    /// <summary>The prepaid balance below which sends are hard-stopped. The deployment names it.</summary>
    public static DecimalSetting AbuseSmsBalanceFloor { get; } =
        new("abuse.sms.balancefloor", SettingScope.Runtime);

    /// <summary>How often the gateway balance is read.</summary>
    public static DurationSetting AbuseSmsPollInterval { get; } =
        new("abuse.sms.pollinterval", SettingScope.Runtime, "PT15M");

    /// <summary>
    /// How far the last hour's messaging spend may exceed the trailing seven-day
    /// hourly mean before the balance alert fires.
    /// </summary>
    public static DecimalSetting AbuseSmsDrainFactor { get; } =
        new(
            "abuse.sms.drainfactor",
            SettingScope.Runtime,
            3.0m,
            floor: 1.0m,
            loosening: SettingDirection.Increase);

    /// <summary>
    /// The signals bot defence counts. The set is closed until a decision adds a
    /// member.
    /// </summary>
    public static MultipleChoiceSetting<BotDefenceSignal> AbuseBotDefenceSignals { get; } =
        new(
            "abuse.botdefence.signals",
            SettingScope.Runtime,
            Enum.GetValues<BotDefenceSignal>().ToFrozenSet(),
            Enum.GetValues<BotDefenceSignal>().ToFrozenSet(),
            FrozenSet<BotDefenceSignal>.Empty,
            loosening: SettingDirection.Decrease);

    /// <summary>Whether an unusual read volume per actor raises an alert.</summary>
    public static FlagSetting ExfiltrationReadVolumeAlerting { get; } =
        new("exfiltration.readvolume.alerting", SettingScope.Runtime, true);

    /// <summary>
    /// Whether a staff bulk export needs step-up. A subject's own export is gated at
    /// the account's reachable assurance instead.
    /// </summary>
    public static FlagSetting ExfiltrationExportStepUpRequired { get; } =
        new("exfiltration.export.stepuprequired", SettingScope.Runtime, true);

    /// <summary>Staff bulk exports admitted in an hour.</summary>
    public static IntegerSetting ExfiltrationExportRateLimit { get; } =
        new("exfiltration.export.ratelimit", SettingScope.Runtime, 5);

    /// <summary>Whether every export is recorded.</summary>
    public static FlagSetting ExfiltrationExportAuditing { get; } =
        new("exfiltration.export.auditing", SettingScope.Protected, true);

    /// <summary>Where alerts are emailed. The deployment names at least one.</summary>
    public static TextListSetting AlertingEmailDestinations { get; } =
        new("alerting.email.destinations", SettingScope.Runtime, minimum: 1);

    /// <summary>Where high-severity alerts are texted. The deployment names at least one.</summary>
    public static TextListSetting AlertingSmsDestinations { get; } =
        new("alerting.sms.destinations", SettingScope.Runtime, minimum: 1);

    /// <summary>
    /// Whether routine alerts reach the owner. Break-glass events reach them either
    /// way.
    /// </summary>
    public static FlagSetting AlertingOwnerEnabled { get; } =
        new("alerting.owner.enabled", SettingScope.Runtime, false);

    /// <summary>The owner's email destination. The deployment names it.</summary>
    public static TextSetting AlertingOwnerEmail { get; } =
        new("alerting.owner.email", SettingScope.Runtime);

    /// <summary>The owner's SMS destination. The deployment names it.</summary>
    public static TextSetting AlertingOwnerSms { get; } =
        new("alerting.owner.sms", SettingScope.Runtime);

    /// <summary>The window a read-volume baseline is drawn from.</summary>
    public static DurationSetting ExfiltrationReadVolumeBaselineWindow { get; } =
        new("exfiltration.readvolume.baselinewindow", SettingScope.Runtime, "P30D");

    /// <summary>
    /// How far an actor's records returned today may exceed their daily mean over the
    /// baseline window before the read-volume alert fires.
    /// </summary>
    public static DecimalSetting ExfiltrationReadVolumeFactor { get; } =
        new(
            "exfiltration.readvolume.factor",
            SettingScope.Runtime,
            3.0m,
            floor: 1.0m,
            loosening: SettingDirection.Increase);

    /// <summary>The count under which no read-volume alert fires.</summary>
    public static IntegerSetting ExfiltrationReadVolumeMinimum { get; } =
        new("exfiltration.readvolume.minimum", SettingScope.Runtime, 500, loosening: SettingDirection.Increase);

    /// <summary>
    /// Failures on one account inside the deduplication window above which sustained
    /// authentication failures are alerted.
    /// </summary>
    public static IntegerSetting AlertingAuthFailuresThreshold { get; } =
        new("alerting.authfailures.threshold", SettingScope.Runtime, 20, loosening: SettingDirection.Increase);

    /// <summary>Recovery requests on one account in a day above which clustering is alerted.</summary>
    public static IntegerSetting AlertingRecoveryAccountThreshold { get; } =
        new("alerting.recovery.accountthreshold", SettingScope.Runtime, 3, loosening: SettingDirection.Increase);

    /// <summary>Approvals by one approver in a day above which their volume is alerted.</summary>
    public static IntegerSetting AlertingRecoveryApproverThreshold { get; } =
        new("alerting.recovery.approverthreshold", SettingScope.Runtime, 3, loosening: SettingDirection.Increase);

    /// <summary>
    /// Permission denials for one actor in a fixed ten-minute window above which a
    /// denial spike is alerted.
    /// </summary>
    public static IntegerSetting AlertingDenialsThreshold { get; } =
        new("alerting.denials.threshold", SettingScope.Runtime, 50, loosening: SettingDirection.Increase);

    /// <summary>
    /// How far apart in kilometres two cities may be before two sessions of one
    /// account used inside the session window are implausible.
    /// </summary>
    public static IntegerSetting AlertingSessionsDistance { get; } =
        new("alerting.sessions.distance", SettingScope.Runtime, 500, loosening: SettingDirection.Increase);

    /// <summary>The window two session uses are compared across.</summary>
    public static DurationSetting AlertingSessionsWindow { get; } =
        new("alerting.sessions.window", SettingScope.Runtime, "PT1H", loosening: SettingDirection.Increase);

    /// <summary>
    /// Non-existence notices an hour, system-wide, above which an enumeration probe is
    /// alerted.
    /// </summary>
    public static IntegerSetting AlertingNonexistentThreshold { get; } =
        new("alerting.nonexistent.threshold", SettingScope.Runtime, 20, loosening: SettingDirection.Increase);

    /// <summary>
    /// Rejected callbacks from one source an hour above which repeated verification
    /// failure is alerted.
    /// </summary>
    public static IntegerSetting AlertingCallbackThreshold { get; } =
        new("alerting.callback.threshold", SettingScope.Runtime, 10, loosening: SettingDirection.Increase);

    /// <summary>How long one condition is reported once.</summary>
    public static DurationSetting AlertingDedupeWindow { get; } =
        new("alerting.dedupe.window", SettingScope.Runtime, "PT1H");

    /// <summary>The severity at which an alert is also texted.</summary>
    public static ChoiceSetting<AlertSeverity> AlertingSmsSeverityThreshold { get; } =
        new(
            "alerting.sms.severitythreshold",
            SettingScope.Runtime,
            AlertSeverity.High,
            Enum.GetValues<AlertSeverity>().ToFrozenSet());

    /// <summary>How far ahead an expiry on the maintenance log is warned about.</summary>
    public static DurationSetting MaintenanceExpiryWarningLead { get; } =
        new("maintenance.expiry.warninglead", SettingScope.Runtime, "P30D");

    /// <summary>
    /// The measurable bound on a reverse lookup, which is the authorization seam's
    /// migration trigger.
    /// </summary>
    public static DurationSetting AuthzReverseLookupBudget { get; } =
        new("authz.reverselookup.budget", SettingScope.Runtime, "PT2S");

    /// <summary>
    /// How often every materialised derivation is re-evaluated against the host's own
    /// relation, a difference corrected and the degradation condition raised.
    /// </summary>
    public static DurationSetting DerivationMaterialisedDriftCheck { get; } =
        new(
            "derivation.materialised.driftcheck",
            SettingScope.Runtime,
            "P1D",
            loosening: SettingDirection.Increase);

    /// <summary>How long an organization's deletion stays cancellable.</summary>
    public static DurationSetting OrganizationDeletionGrace { get; } =
        new("organization.deletion.grace", SettingScope.Runtime, "P30D", floor: "P7D");

    /// <summary>From a takedown to its erasure, during which it can be reversed.</summary>
    public static DurationSetting TakedownGrace { get; } =
        new("takedown.grace", SettingScope.Runtime, "P7D", floor: "P7D");

    /// <summary>How long an account's deletion stays cancellable.</summary>
    public static DurationSetting AccountDeletionGrace { get; } =
        new("account.deletion.grace", SettingScope.Runtime, "P30D", floor: "P7D");

    /// <summary>Whether one account may hold memberships in several organizations.</summary>
    public static FlagSetting OrganizationMultipleMemberships { get; } =
        new("organization.multiplememberships", SettingScope.Runtime, false);

    /// <summary>The undo window after an identifier is removed or replaced.</summary>
    public static DurationSetting IdentifierChangeCoolingOff { get; } =
        new("identifier.change.coolingoff", SettingScope.Runtime, "PT72H", floor: "PT72H");

    /// <summary>
    /// Whether an account has to hold a verified phone. Phone is never the sole
    /// identifier.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> RegistrationPhone { get; } =
        new(
            "registration.phone",
            SettingScope.Runtime,
            AttributeRequirement.Required,
            new[] { AttributeRequirement.Required, AttributeRequirement.Optional }.ToFrozenSet(),
            loosening: SettingDirection.Decrease);

    /// <summary>
    /// Whether an under-age date ends the registration session, or only records the
    /// age group.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> RegistrationAdultAffirmation { get; } =
        new(
            "registration.adultaffirmation",
            SettingScope.Runtime,
            AttributeRequirement.Required,
            new[] { AttributeRequirement.Required, AttributeRequirement.Off }.ToFrozenSet());

    /// <summary>
    /// How long a registration session lives before it is swept, leaving nothing.
    /// </summary>
    public static DurationSetting RegistrationSessionLifetime { get; } =
        new("registration.session.lifetime", SettingScope.Runtime, "PT24H", ceiling: "PT72H");

    /// <summary>
    /// How often the waiting screen's stream reads the registration state back where
    /// no signal has reached it. The floor is the interval: a press has to feel
    /// immediate to the person waiting, and the signal is what usually answers first.
    /// </summary>
    public static DurationSetting RegistrationEventsPollInterval { get; } =
        new("registration.events.pollinterval", SettingScope.Runtime, "PT1S", floor: "PT1S");

    /// <summary>
    /// Verified email addresses an account may hold. One is single-address mode, where
    /// a replacement happens in one operation.
    /// </summary>
    public static IntegerSetting IdentifiersEmailMax { get; } =
        new("identifiers.email.max", SettingScope.Runtime, 10, floor: 1, loosening: SettingDirection.Increase);

    /// <summary>Verified phone numbers an account may hold.</summary>
    public static IntegerSetting IdentifiersPhoneMax { get; } =
        new("identifiers.phone.max", SettingScope.Runtime, 10, floor: 1, loosening: SettingDirection.Increase);

    /// <summary>
    /// Whether usernames exist. While off, no request accepts one and no response
    /// carries the field.
    /// </summary>
    public static FlagSetting IdentifiersUsernameEnabled { get; } =
        new("identifiers.username.enabled", SettingScope.Runtime, false);

    /// <summary>The shortest interval between username changes.</summary>
    public static DurationSetting IdentifiersUsernameChangeCoolOff { get; } =
        new("identifiers.username.changecooloff", SettingScope.Runtime, "P30D", floor: "P1D");

    /// <summary>
    /// Whether a legal name is collected, which is a proofing attribute collected
    /// only with a declared purpose.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> ProfileLegalName { get; } =
        new(
            "profile.legalname",
            SettingScope.Runtime,
            AttributeRequirement.Off,
            Enum.GetValues<AttributeRequirement>().ToFrozenSet());

    /// <summary>
    /// Whether the date entered at the age step is retained. The date is immutable to
    /// the person.
    /// </summary>
    public static ChoiceSetting<AttributeRequirement> ProfileDateOfBirth { get; } =
        new(
            "profile.dateofbirth",
            SettingScope.Runtime,
            AttributeRequirement.Off,
            Enum.GetValues<AttributeRequirement>().ToFrozenSet());

    /// <summary>How often the sweep re-verifies a locked domain's record.</summary>
    public static DurationSetting DomainReverifyInterval { get; } =
        new(
            "domain.reverify.interval",
            SettingScope.Runtime,
            "P1D",
            floor: "PT1H",
            loosening: SettingDirection.Increase);

    /// <summary>The outbox publisher's cadence.</summary>
    public static DurationSetting OutboxPollInterval { get; } =
        new("outbox.poll.interval", SettingScope.Runtime, "PT5S");

    /// <summary>The first retry delay, to which full jitter is applied.</summary>
    public static DurationSetting OutboxRetryInitial { get; } =
        new("outbox.retry.initial", SettingScope.Runtime, "PT30S");

    /// <summary>What the retry delay is multiplied by on each further attempt.</summary>
    public static DecimalSetting OutboxRetryFactor { get; } =
        new("outbox.retry.factor", SettingScope.Runtime, 2.0m, floor: 1.0m);

    /// <summary>Attempts before a delivery is failed and its exhaustion alerted.</summary>
    public static IntegerSetting OutboxRetryMaxAttempts { get; } =
        new("outbox.retry.maxattempts", SettingScope.Runtime, 10, floor: 1);

    /// <summary>
    /// The interval of the one sweep, and so the longest a deadline waits past its
    /// instant.
    /// </summary>
    public static DurationSetting SweepInterval { get; } =
        new("sweep.interval", SettingScope.Runtime, "PT5M", ceiling: "PT15M");

    /// <summary>
    /// The deployment's message languages, in which every template is validated at
    /// startup. The deployment names at least one.
    /// </summary>
    public static TextListSetting NotificationLanguages { get; } =
        new("notification.languages", SettingScope.Runtime, minimum: 1);

    /// <summary>The domain outbound mail is sent from. The deployment names it.</summary>
    public static TextSetting NotificationEmailSendingDomain { get; } =
        new("notification.email.sendingdomain", SettingScope.Runtime);

    /// <summary>The domains registered with the private relay.</summary>
    public static TextSetSetting NotificationEmailRelayRegistered { get; } =
        new("notification.email.relayregistered", SettingScope.Runtime, FrozenSet<string>.Empty);

    /// <summary>How often the location database is refreshed.</summary>
    public static DurationSetting LocationDatabaseRefresh { get; } =
        new("location.database.refresh", SettingScope.Runtime, "P7D");

    /// <summary>
    /// The age beyond which the location database is stale, no location is shown and
    /// a degradation is raised.
    /// </summary>
    public static DurationSetting LocationDatabaseMaxAge { get; } =
        new("location.database.maxage", SettingScope.Runtime, "P30D");

    /// <summary>
    /// The word the context source forbids in passwords. The deployment names it
    /// where that source is on.
    /// </summary>
    public static TextSetting ServiceName { get; } =
        new("service.name", SettingScope.Runtime);

    /// <summary>
    /// The cap in bytes on the whole set of host-declared preference values for one
    /// account.
    /// </summary>
    public static IntegerSetting PreferencesMaxSize { get; } =
        new("preferences.maxsize", SettingScope.Runtime, 8192, ceiling: 65536);

    /// <summary>
    /// Working days from a privacy request's submission to its decision. A lapse is a
    /// deemed rejection, so only a shorter deadline than the statutory period is
    /// configurable.
    /// </summary>
    public static IntegerSetting PrivacyRequestDecision { get; } =
        new("privacy.request.decision", SettingScope.Runtime, 6, ceiling: 6);

    /// <summary>
    /// The zone in which calendar days, working days and holidays are determined. The
    /// deployment names it.
    /// </summary>
    public static TextSetting PrivacyCalendarTimeZone { get; } =
        new("privacy.calendar.timezone", SettingScope.Protected);

    /// <summary>The week on which working days are counted.</summary>
    public static MultipleChoiceSetting<DayOfWeek> PrivacyWorkingDays { get; } =
        new(
            "privacy.workingdays",
            SettingScope.Runtime,
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
        new("privacy.holidays", SettingScope.Runtime);

    /// <summary>
    /// Working days before a decision deadline at which it is warned about.
    /// </summary>
    public static IntegerSetting PrivacyRequestWarningLead { get; } =
        new("privacy.request.warninglead", SettingScope.Runtime, 2);

    /// <summary>
    /// How long security events, permission changes and financial actions are kept.
    /// </summary>
    public static DurationSetting RetentionAuditSecurity { get; } =
        new("retention.audit.security", SettingScope.Runtime, "P7Y", floor: "P5Y");

    /// <summary>How long routine access logging is kept.</summary>
    public static DurationSetting RetentionAuditRoutine { get; } =
        new("retention.audit.routine", SettingScope.Runtime, "P90D", floor: "P30D");

    /// <summary>
    /// How long a consent record is kept after the processing it covered ends.
    /// </summary>
    public static DurationSetting RetentionConsent { get; } =
        new("retention.consent", SettingScope.Runtime, "P3Y", floor: "P1Y");

    /// <summary>
    /// The free-text hosting environment of the records of processing. The deployment
    /// names it where it generates the register.
    /// </summary>
    public static TextSetting HostingEnvironment { get; } =
        new("hosting.environment", SettingScope.Runtime);

    /// <summary>
    /// The recovery-time objective the automatic restore test is measured against,
    /// and the upper bound of the accepted objective.
    /// </summary>
    public static DurationSetting BackupRestoreTestObjective { get; } =
        new("backup.restoretest.objective", SettingScope.Runtime, "PT8H", ceiling: "PT8H");

    /// <summary>How often the restore test runs.</summary>
    public static DurationSetting BackupRestoreTestInterval { get; } =
        new("backup.restoretest.interval", SettingScope.Runtime, "P3M", ceiling: "P3M");

    /// <summary>
    /// The canary subject the restore test decrypts a field of and resolves the
    /// fingerprint of. Bootstrap seeds it.
    /// </summary>
    public static TextSetting BackupRestoreTestCanary { get; } =
        new("backup.restoretest.canary", SettingScope.Runtime, string.Empty);

    /// <summary>
    /// How long a backup is kept, which also bounds how long a pre-erasure backup
    /// survives.
    /// </summary>
    public static DurationSetting BackupRetention { get; } =
        new("backup.retention", SettingScope.Runtime, "P35D", floor: "P14D");

    /// <summary>
    /// Whether the deployment is hosted inside or outside Egypt. The deployment names
    /// it, and an outside value makes the cross-border basis required.
    /// </summary>
    public static ChoiceSetting<HostingLocation> HostingLocation { get; } =
        new("hosting.location", SettingScope.Protected, Enum.GetValues<HostingLocation>().ToFrozenSet());

    /// <summary>
    /// The basis for hosting outside Egypt, which the deployment names when it hosts
    /// there.
    /// </summary>
    public static TextSetting HostingCrossBorderBasis { get; } =
        new("hosting.crossborderbasis", SettingScope.Protected);

    /// <summary>
    /// The default governing language of every legal document version. The deployment
    /// names it.
    /// </summary>
    public static TextSetting LegalGoverningLanguage { get; } =
        new("legal.governinglanguage", SettingScope.Protected);

    /// <summary>Whether the audit log is written.</summary>
    public static FlagSetting AuditEnabled { get; } =
        new("audit.enabled", SettingScope.Protected, true);

    /// <summary>Whether a token's signature is verified.</summary>
    public static FlagSetting TokenSignatureVerification { get; } =
        new("token.signature.verification", SettingScope.Protected, true);

    /// <summary>
    /// How long an access token lives, which for a relying party that validates
    /// offline is the revocation latency.
    /// </summary>
    public static DurationSetting OidcAccessTokenLifetime { get; } =
        new(
            "oidc.accesstoken.lifetime",
            SettingScope.Runtime,
            "PT10M",
            ceiling: "PT1H",
            loosening: SettingDirection.Increase);

    /// <summary>How long an authorization code lives.</summary>
    public static DurationSetting OidcCodeLifetime { get; } =
        new("oidc.code.lifetime", SettingScope.Runtime, "PT60S", ceiling: "PT10M");

    /// <summary>
    /// The algorithm tokens are signed with. The one place the value is held.
    /// </summary>
    public static ChoiceSetting<string> TokenSigningAlgorithm { get; } =
        new(
            "token.signing.algorithm",
            SettingScope.Protected,
            "ES256",
            new[] { "ES256" }.ToFrozenSet(StringComparer.Ordinal));

    /// <summary>
    /// How often the signing key is rotated. The overlap is the access-token lifetime
    /// plus five minutes, and is not a key of its own.
    /// </summary>
    public static DurationSetting TokenSigningRotation { get; } =
        new("token.signing.rotation", SettingScope.Runtime, "P90D");

    /// <summary>
    /// What an organization changes about the system policy: one key per organization,
    /// created with no override when the organization is.
    /// </summary>
    public static SettingFamily<PolicyOverride> OrganizationPolicy { get; } =
        new("policy", SettingScope.Runtime, SettingForms.Override, PolicyOverride.None);

    /// <summary>
    /// How long a host-declared category of data is kept: one key per declared
    /// category, whose floor the host declares. Startup fails for a declared category
    /// without one.
    /// </summary>
    public static SettingFamily<TimeSpan> HostCategoryRetention { get; } =
        new("retention", SettingScope.Runtime, SettingForms.Duration);

    /// <summary>
    /// Whether step-up is enforced for an organization: one key per organization, and
    /// the protected kill switch rather than a field of the policy object.
    /// </summary>
    public static SettingFamily<bool> OrganizationStepUpEnforcement { get; } =
        new("stepup.enforcement", SettingScope.Protected, SettingForms.Flag, true);

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
        PasswordBlocklistSelfHostedAddress,
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
        RecoveryInvalidationNoticeInterval,
        RecoveryRateLimitAccount,
        RecoveryRateLimitApprover,
        BreakGlassSessionLifetime,
        AbuseThrottleEnabled,
        AbuseThrottleThreshold,
        AbuseThrottleDelayInitial,
        AbuseThrottleDelayFactor,
        AbuseThrottleDelayMax,
        AbuseThrottleAccountCap,
        AbuseThrottleDecay,
        AbuseNonexistentWindow,
        AbuseSourceRateLimit,
        AbuseBotDefenceRepeatedAttempts,
        IntegrationCallbackRateLimit,
        IntegrationMailEndpoint,
        IntegrationSmsEndpoint,
        Restrictions,
        CodeVerificationLifetime,
        CodeVerificationAttempts,
        LinkMagicLifetime,
        LinkInvitationLifetime,
        PhotoMaxBytes,
        PhotoMaxDimension,
        AbuseSmsBalanceFloor,
        AbuseSmsPollInterval,
        AbuseSmsDrainFactor,
        AbuseBotDefenceSignals,
        ExfiltrationReadVolumeAlerting,
        ExfiltrationExportStepUpRequired,
        ExfiltrationExportRateLimit,
        ExfiltrationExportAuditing,
        AlertingEmailDestinations,
        AlertingSmsDestinations,
        AlertingOwnerEnabled,
        AlertingOwnerEmail,
        AlertingOwnerSms,
        ExfiltrationReadVolumeBaselineWindow,
        ExfiltrationReadVolumeFactor,
        ExfiltrationReadVolumeMinimum,
        AlertingAuthFailuresThreshold,
        AlertingRecoveryAccountThreshold,
        AlertingRecoveryApproverThreshold,
        AlertingDenialsThreshold,
        AlertingSessionsDistance,
        AlertingSessionsWindow,
        AlertingNonexistentThreshold,
        AlertingCallbackThreshold,
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
        RegistrationEventsPollInterval,
        IdentifiersEmailMax,
        IdentifiersPhoneMax,
        IdentifiersUsernameEnabled,
        IdentifiersUsernameChangeCoolOff,
        ProfileLegalName,
        ProfileDateOfBirth,
        DomainReverifyInterval,
        OutboxPollInterval,
        OutboxRetryInitial,
        OutboxRetryFactor,
        OutboxRetryMaxAttempts,
        SweepInterval,
        NotificationLanguages,
        NotificationEmailSendingDomain,
        NotificationEmailRelayRegistered,
        LocationDatabaseRefresh,
        LocationDatabaseMaxAge,
        ServiceName,
        PreferencesMaxSize,
        PrivacyRequestDecision,
        PrivacyCalendarTimeZone,
        PrivacyWorkingDays,
        PrivacyHolidays,
        PrivacyRequestWarningLead,
        RetentionAuditSecurity,
        RetentionAuditRoutine,
        RetentionConsent,
        HostingEnvironment,
        BackupRestoreTestObjective,
        BackupRestoreTestInterval,
        BackupRestoreTestCanary,
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
    /// library cannot guess them. Three of them are named only where their condition
    /// holds, which <see cref="ThrowIfIncomplete"/> reads.
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
    /// <param name="blocklistSource">The value named for <c>password.blocklist.source</c>.</param>
    /// <param name="blocklistSources">The value named for <c>password.blocklist.sources</c>.</param>
    /// <param name="recordsOfProcessing">
    /// Whether the deployment generates the records of processing.
    /// </param>
    /// <exception cref="ArgumentNullException">A set the check reads is absent.</exception>
    /// <exception cref="StartupException">
    /// A key the deployment has to name carries no value. The exception names the key
    /// under <c>details.key</c> and carries the code chapter 10 section 1.5 gives the
    /// condition.
    /// </exception>
    /// <remarks>
    /// Implements LIB-HOST-001, INT-HOST-001, PRIV-ROPA-001, AUTH-PASS-004,
    /// CONV-ERR-001. Every other key resolves with its default, so a deployment that
    /// names these and nothing else starts.
    /// </remarks>
    public static void ThrowIfIncomplete(
        IReadOnlySet<ConfigurationKey> named,
        HostingLocation? location,
        BlocklistSource blocklistSource,
        IReadOnlySet<BlocklistRejectionSource> blocklistSources,
        bool recordsOfProcessing)
    {
        ArgumentNullException.ThrowIfNull(named);
        ArgumentNullException.ThrowIfNull(blocklistSources);

        if (!named.Contains(LegalGoverningLanguage.Key))
        {
            throw Unnamed(LegalGoverningLanguage.Key, ErrorCodes.StartupGoverningLanguage);
        }

        foreach (Setting setting in Required)
        {
            if (!Applies(setting, location, blocklistSource, blocklistSources, recordsOfProcessing))
            {
                continue;
            }

            if (!named.Contains(setting.Key))
            {
                throw Unnamed(setting.Key, ErrorCodes.StartupDeclarationMissing);
            }
        }
    }

    // The conditional declarations of the section 4 preamble. Every other required
    // key is named whatever the deployment does.
    private static bool Applies(
        Setting setting,
        HostingLocation? location,
        BlocklistSource blocklistSource,
        IReadOnlySet<BlocklistRejectionSource> blocklistSources,
        bool recordsOfProcessing)
    {
        if (setting.Key == HostingCrossBorderBasis.Key)
        {
            return location == Configuration.HostingLocation.Outside;
        }

        if (setting.Key == PasswordBlocklistSelfHostedAddress.Key)
        {
            return blocklistSource is BlocklistSource.SelfHosted;
        }

        if (setting.Key == ServiceName.Key)
        {
            return blocklistSources.Contains(BlocklistRejectionSource.Context);
        }

        return setting.Key != HostingEnvironment.Key || recordsOfProcessing;
    }

    // The fault a missing declaration raises, naming the key it is missing.
    private static StartupException Unnamed(ConfigurationKey key, ErrorCode code) =>
        new(
            "The deployment names " + key + "; no value was supplied.",
            new Error(
                code,
                new Dictionary<string, JsonElement>(capacity: 1, StringComparer.Ordinal)
                {
                    ["key"] = JsonSerializer.SerializeToElement(key.ToString()),
                }));

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
