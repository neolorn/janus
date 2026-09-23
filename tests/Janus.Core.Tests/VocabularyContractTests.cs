using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The names the vocabularies of chapter 10 section 5 carry on the wire. They are part
/// of the stable contract: a host reads them from a policy and an endpoint declares
/// them, so a rename is a breaking change and fails here (LIB-API-001).
/// </summary>
[Trait("kind", "contract")]
public sealed class VocabularyContractTests
{
    // Chapter 10 section 5a, in the order the table gives them.
    private static readonly string[] StepUpActions =
    [
        "password:set",
        "identifier:add",
        "identifier:remove",
        "username:change",
        "factor:enrol",
        "factor:remove",
        "recoverycodes:generate",
        "mailcredential:create",
        "mailcredential:revoke",
        "privacy:export",
        "account:delete",
        "account:deactivate",
        "provider:link",
        "provider:unlink",
        "recovery:approve",
        "invitation:issue",
        "grant:manage",
        "account:suspend",
        "account:reactivate",
        "account:takedown",
        "account:takedownreverse",
        "erasure:complete",
        "config:loosen",
        "alerting:destinations",
        "policy:change",
        "domain:manage",
        "restriction:edit",
        "restriction:grant",
        "breakglass:replace",
        "organization:delete",
    ];

    // The catalogue of chapter 02 AUTH-FACT-002 that carries an identifier.
    private static readonly string[] Factors =
    [
        "password",
        "passkey",
        "emailLink",
        "emailCode",
        "phoneLink",
        "google",
        "apple",
        "totp",
        "securityKey",
        "phoneCode",
        "recoveryCodes",
        "breakGlass",
    ];

    // Chapter 10 section 5.23, in the order the OPS-ALERT-001 table gives them.
    private static readonly string[] AlertConditions =
    [
        "auth-failures-sustained",
        "recovery-clustering",
        "approver-volume",
        "read-volume-anomaly",
        "breakglass-used",
        "protected-setting-changed",
        "alert-destination-changed",
        "stepup-policy-weakened",
        "concurrent-sessions-implausible",
        "denial-spike",
        "sms-balance",
        "background-job-failed",
        "erasure-delivery-exhausted",
        "certificate-renewal-failed",
        "clock-drift",
        "degradation",
        "callback-verification-failed",
        "nonexistent-notice-rate",
        "privacy-deadline-approaching",
        "privacy-deadline-reached",
        "expiry-approaching",
        "holiday-list-exhausted",
        "no-emergency-credential",
        "restore-test-failed",
        "restriction-loosened",
        "restriction-granted",
        "domain-reverification-failed",
        "domain-removed",
        "governing-language-changed",
        "governing-text-missing",
        "relay-domain-unregistered",
    ];

    /// <summary>
    /// LIB-API-001 AC2: the step-up action names are the keys of a policy's gates, so
    /// the set is fixed and a rename is caught here.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheStepUpActionNamesAreTheContract() =>
        Assert.Equal(StepUpActions.Order(StringComparer.Ordinal), WireNames<StepUpAction>());

    /// <summary>
    /// LIB-API-001 AC2: the factor identifiers are the members of a policy's login
    /// factors and the values an endpoint declares.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheFactorIdentifiersAreTheContract() =>
        Assert.Equal(Factors.Order(StringComparer.Ordinal), WireNames<Factor>());

    /// <summary>
    /// LIB-API-001 AC2: the assurance levels of chapter 10 section 5.4, the gate levels
    /// of section 4.1a and the redundancy values it names.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_ThePolicyFieldValuesAreTheContract()
    {
        Assert.Equal(["aal1", "aal2", "aal3", "delegated"], WireNames<AssuranceLevel>());
        Assert.Equal(["aal1", "aal2", "reachable"], WireNames<GateLevel>());
        Assert.Equal(["advisory", "enforced"], WireNames<CredentialRedundancy>());
    }

    /// <summary>
    /// LIB-API-001 AC2: the closed sets of chapter 10 sections 5.20 to 5.22, which a
    /// capability, a consent record and an age screen carry.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheClosedSetsOfSectionFiveAreTheContract()
    {
        Assert.Equal(
            ["accountstate", "consent", "reauthenticate", "restricted", "stepup"],
            WireNames<CapabilityResidual>());
        Assert.Equal(
            ["administrator", "dashboard", "reconsent", "registration"],
            WireNames<ConsentMechanism>());
        Assert.Equal(["adult", "minor"], WireNames<AgeGroup>());
    }

    /// <summary>
    /// LIB-API-001 AC2: the subject types of chapter 10 section 5.5, the grant kinds of
    /// section 5.6 and the concealment behaviour of section 5.11, which every grant row
    /// and every resource type declaration carries.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheAuthorizationVocabulariesAreTheContract()
    {
        Assert.Equal(["group", "user"], WireNames<SubjectType>());
        Assert.Equal(["derived", "materialised", "stored"], WireNames<GrantKind>());
        Assert.Equal(["conceal", "disclose"], WireNames<ConcealmentBehaviour>());
    }

    /// <summary>
    /// LIB-API-001 AC2: the session types of chapter 10 section 5.2, the
    /// authenticator states of section 5.3a and the reauthentication kinds of section
    /// 1.2, which a session record, a credential row and an expiry each carry.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheSessionVocabulariesAreTheContract()
    {
        Assert.Equal(["auth", "oidc-token", "per-app"], WireNames<SessionType>());
        Assert.Equal(["active", "invalidated", "suspended"], WireNames<AuthenticatorState>());
        Assert.Equal(["full", "single-factor"], WireNames<ReauthenticationKind>());
        Assert.Equal(["remembered", "trusted"], WireNames<DeviceKind>());
        Assert.Equal(["credentialRedundancy", "requiredAssurance"], WireNames<PolicyField>());
    }

    /// <summary>
    /// LIB-API-001 AC2: the alert conditions of chapter 10 section 5.23, one per
    /// OPS-ALERT-001 row, which an alert carries and deduplicates on.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheAlertConditionsAreTheContract() =>
        Assert.Equal(AlertConditions.Order(StringComparer.Ordinal), WireNames<AlertCondition>());

    /// <summary>
    /// LIB-API-001 AC2: the identifier kinds of chapter 10 section 5.17, which every
    /// identifier row carries and every identifier endpoint names.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheIdentifierKindsAreTheContract() =>
        Assert.Equal(["email", "phone", "username"], WireNames<IdentifierKind>());

    /// <summary>
    /// LIB-API-001 AC2: the account states of chapter 10 section 5.1, which every
    /// account row carries and every reader of an account branches on.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheAccountStatesAreTheContract() =>
        Assert.Equal(
            ["active", "deleted", "deleting", "restricted", "suspended"],
            WireNames<AccountState>());

    /// <summary>
    /// LIB-API-001 AC2: the origins of chapter 10 section 5.12b and the takedown
    /// triggers of section 5.12d, recorded when a state is entered.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheSuspensionAndDeletionOriginsAreTheContract()
    {
        Assert.Equal(["administrator", "self"], WireNames<SuspensionOrigin>());
        Assert.Equal(["oob-request", "self", "takedown"], WireNames<DeletionOrigin>());
        Assert.Equal(
            ["authority-request", "automated-signal", "customer-report", "staff-report"],
            WireNames<TakedownTrigger>());
    }

    /// <summary>
    /// LIB-API-001 AC2: the erasure status of chapter 10 section 5.12 and the reason of
    /// section 5.12a, which the erasures table and the off-host ledger carry.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheErasureStatusAndReasonAreTheContract()
    {
        Assert.Equal(
            ["awaiting-subscribers", "complete", "failed"],
            WireNames<ErasureStatus>());
        Assert.Equal(
            ["erasure-request", "minor-takedown", "organization-erasure"],
            WireNames<ErasureReason>());
    }

    /// <summary>
    /// LIB-API-001 AC2: the two members of the bot-defence signal set, which chapter
    /// 10 section 4.5 closes until a decision adds one.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheBotDefenceSignalsAreTheContract() =>
        Assert.Equal(["datacenterRange", "repeatedAttempts"], WireNames<BotDefenceSignal>());

    /// <summary>
    /// LIB-API-001 AC2: the restriction keys of chapter 10 section 5.14, the purposes
    /// of section 5.15 and the bucket windows of section 5.16, which every declared
    /// restriction carries, with the severities of OPS-ALERT-001 beside them.
    /// </summary>
    [Fact]
    public void LIB_API_001_AC2_TheRestrictionVocabulariesAreTheContract()
    {
        Assert.Equal(
            ["account", "destination", "global", "host", "source"],
            WireNames<RestrictionKeyKind>());
        Assert.Equal(
            ["any", "notification", "secondfactor", "signin", "verification"],
            WireNames<RestrictionPurpose>());
        Assert.Equal(["fixed", "sliding"], WireNames<BucketWindow>());
        Assert.Equal(["high", "normal"], WireNames<AlertSeverity>());
    }

    /// <summary>
    /// REG-PREF-001: the four types a host declares a preference key with, spelled as
    /// the item spells them, because a host writes the declaration in those words.
    /// </summary>
    [Fact]
    public void REG_PREF_001_ThePreferenceTypesAreTheOnesTheItemNames() =>
        Assert.Equal(
            ["boolean", "enum", "integer", "string"],
            WireNames<PreferenceKind>());

    /// <summary>
    /// The catalogue a deployment declares is asked by message and by channel, so
    /// what the library asks it for is a written name and never the compiler's
    /// (CONV-CONTENT-001, LIB-HOST-001).
    /// </summary>
    [Fact]
    public void WireNames_TheKeysTheCatalogueIsAskedBy_AreWritten()
    {
        Assert.Equal(
            [
                "account-exists",
                "alert",
                "credential-enrolled",
                "deactivation-notice",
                "deletion-notice",
                "enrolment-link",
                "identifier-added",
                "identifier-change-confirm",
                "identifier-detached",
                "identifier-removed",
                "identifier-settings-changed",
                "no-account",
                "privacy-request-lapsed",
                "privacy-request-received",
                "recovery-link",
                "secondstep-code",
                "security-notice",
                "signin-link",
                "verification-code",
            ],
            WireNames<MessageKind>());
        Assert.Equal(["email", "sms"], WireNames<SendKind>());
    }

    /// <summary>
    /// Every member of every vocabulary carries a wire name, so none of them reaches a
    /// host as the name the compiler happens to give it.
    /// </summary>
    [Fact]
    public void WireNames_EveryVocabularyMember_CarriesOne()
    {
        Type[] vocabularies =
        [
            typeof(StepUpAction),
            typeof(Factor),
            typeof(AssuranceLevel),
            typeof(GateLevel),
            typeof(CredentialRedundancy),
            typeof(CapabilityResidual),
            typeof(ConsentMechanism),
            typeof(AgeGroup),
            typeof(AlertCondition),
            typeof(BotDefenceSignal),
            typeof(AccountState),
            typeof(SuspensionOrigin),
            typeof(DeletionOrigin),
            typeof(ErasureStatus),
            typeof(ErasureReason),
            typeof(TakedownTrigger),
            typeof(SessionType),
            typeof(AuthenticatorState),
            typeof(ReauthenticationKind),
            typeof(DeviceKind),
            typeof(PolicyField),
            typeof(RestrictionKeyKind),
            typeof(RestrictionPurpose),
            typeof(BucketWindow),
            typeof(AlertSeverity),
            typeof(MessageKind),
            typeof(PreferenceKind),
            typeof(SendKind),
            typeof(RegistrationStep),
        ];

        foreach (Type vocabulary in vocabularies)
        {
            foreach (string member in Enum.GetNames(vocabulary))
            {
                Assert.NotNull(
                    vocabulary.GetField(member)!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>());
            }
        }
    }

    private static string[] WireNames<TVocabulary>()
        where TVocabulary : struct, Enum =>
        [.. Enum.GetNames<TVocabulary>()
            .Select(member => typeof(TVocabulary)
                .GetField(member)!
                .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!
                .Name)
            .Order(StringComparer.Ordinal)];
}
