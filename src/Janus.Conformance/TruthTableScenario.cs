using System.Text.Json.Serialization;

namespace Janus.Conformance;

/// <summary>
/// How the person a truth-table case asks about stands to the record: the
/// relationship and the condition the case writes before the record is asked about.
/// </summary>
/// <remarks>
/// Implements AUTHZ-TEST-001 AC1. Each scenario is written the same way for every
/// resource type, from what the type's declaration says of its containment and its
/// derivations, so a case states only the scenario, the permission and the outcome.
/// Every role a case names allows the case's permission, but for
/// <see cref="RoleWithoutPermission"/>, whose role allows nothing. A step-up scenario
/// is a grant on the record at an action bound to a step-up gate, judged from the
/// report of an assurance provider of the suite's own (LIB-TEST-001 AC2, LIB-HOST-004).
/// </remarks>
public enum TruthTableScenario
{
    /// <summary>
    /// A grant on the record itself.
    /// </summary>
    [JsonStringEnumMemberName("grant-on-record")]
    GrantOnRecord = 0,

    /// <summary>
    /// A grant on the record's container. The type is contained in another.
    /// </summary>
    [JsonStringEnumMemberName("grant-on-container")]
    GrantOnContainer = 1,

    /// <summary>
    /// A grant on the container of the record's container. The type is contained two
    /// levels deep.
    /// </summary>
    [JsonStringEnumMemberName("grant-above-container")]
    GrantAboveContainer = 2,

    /// <summary>
    /// A grant on the whole organization owning the record.
    /// </summary>
    [JsonStringEnumMemberName("grant-on-organization")]
    GrantOnOrganization = 3,

    /// <summary>
    /// A grant on another record of the same type in the same place.
    /// </summary>
    [JsonStringEnumMemberName("grant-on-sibling")]
    GrantOnSibling = 4,

    /// <summary>
    /// No grant at all.
    /// </summary>
    [JsonStringEnumMemberName("no-grant")]
    NoGrant = 5,

    /// <summary>
    /// A grant on the record to a group the person belongs to.
    /// </summary>
    [JsonStringEnumMemberName("grant-to-group")]
    GrantToGroup = 6,

    /// <summary>
    /// A grant on the record to a group holding a group the person belongs to.
    /// </summary>
    [JsonStringEnumMemberName("grant-to-nested-group")]
    GrantToNestedGroup = 7,

    /// <summary>
    /// A deny on the record over a grant on the whole organization.
    /// </summary>
    [JsonStringEnumMemberName("deny-over-grant")]
    DenyOverGrant = 8,

    /// <summary>
    /// A deny on the record's container over a grant on the record. The type is
    /// contained in another.
    /// </summary>
    [JsonStringEnumMemberName("deny-on-container-over-grant")]
    DenyOnContainerOverGrant = 9,

    /// <summary>
    /// A grant on the record that has expired.
    /// </summary>
    [JsonStringEnumMemberName("expired-grant")]
    ExpiredGrant = 10,

    /// <summary>
    /// A grant on the record that was revoked.
    /// </summary>
    [JsonStringEnumMemberName("revoked-grant")]
    RevokedGrant = 11,

    /// <summary>
    /// A grant on the record recorded in another organization.
    /// </summary>
    [JsonStringEnumMemberName("grant-in-another-organization")]
    GrantInAnotherOrganization = 12,

    /// <summary>
    /// A grant on the record whose role allows nothing.
    /// </summary>
    [JsonStringEnumMemberName("role-without-permission")]
    RoleWithoutPermission = 13,

    /// <summary>
    /// A fact in the host's data about the record itself, conferring a role through a
    /// derivation its type declares.
    /// </summary>
    [JsonStringEnumMemberName("derived-grant")]
    DerivedGrant = 14,

    /// <summary>
    /// A fact in the host's data about a container of the record, conferring a role
    /// through a derivation the container's type declares.
    /// </summary>
    [JsonStringEnumMemberName("derived-grant-on-container")]
    DerivedGrantOnContainer = 15,

    /// <summary>
    /// A deny on the record over a role a fact in the host's data confers: the fact on
    /// the record where its type declares a derivation, and on the nearest container
    /// whose type does otherwise.
    /// </summary>
    [JsonStringEnumMemberName("deny-over-derived-grant")]
    DenyOverDerivedGrant = 16,

    /// <summary>
    /// A grant on the record, at an action bound to a step-up gate, where the assurance
    /// report meets the gate.
    /// </summary>
    [JsonStringEnumMemberName("stepup-met")]
    StepUpMet = 17,

    /// <summary>
    /// A grant on the record, at an action bound to a step-up gate, where the assurance
    /// report is below the level the gate asks.
    /// </summary>
    [JsonStringEnumMemberName("stepup-level-unmet")]
    StepUpLevelUnmet = 18,

    /// <summary>
    /// A grant on the record, at an action bound to a step-up gate, where the assurance
    /// report never reached phishing resistance.
    /// </summary>
    [JsonStringEnumMemberName("stepup-phishingresistance-unmet")]
    StepUpPhishingResistanceUnmet = 19,

    /// <summary>
    /// A grant on the record, at an action bound to a step-up gate, where the assurance
    /// report is older than the gate's maximum age.
    /// </summary>
    [JsonStringEnumMemberName("stepup-age-unmet")]
    StepUpAgeUnmet = 20,

    /// <summary>
    /// A grant on the record, at an action bound to a step-up gate, where the assurance
    /// report names an instant after now.
    /// </summary>
    [JsonStringEnumMemberName("stepup-instant-future")]
    StepUpInstantFuture = 21,

    /// <summary>
    /// A grant on the record, at an action bound to a step-up gate, where the assurance
    /// provider fails to give a report.
    /// </summary>
    [JsonStringEnumMemberName("stepup-provider-failed")]
    StepUpProviderFailed = 22,

    /// <summary>
    /// A grant on the record, at an action bound to a step-up gate, where the deployment
    /// registers no assurance provider.
    /// </summary>
    [JsonStringEnumMemberName("stepup-provider-absent")]
    StepUpProviderAbsent = 23,
}
