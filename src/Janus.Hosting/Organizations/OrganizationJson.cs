using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Organizations;

/// <summary>
/// How the administration of organizations reads and writes, generated rather than
/// reflected over (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>
/// Implements IDN-ORG-002, IDN-ORG-003, IDN-ORG-006, IDN-LIFE-009a, IDN-READ-001,
/// IDN-READ-002, AUTH-STEP-002a and API-CONV-002.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CreatedOrganizationView))]
[JsonSerializable(typeof(OrganizationBody))]
[JsonSerializable(typeof(OrganizationReasonBody))]
[JsonSerializable(typeof(MembershipEndBody))]
[JsonSerializable(typeof(OrganizationPolicyView))]
[JsonSerializable(typeof(OrganizationDomainBody))]
[JsonSerializable(typeof(OrganizationDomainView))]
[JsonSerializable(typeof(IReadOnlyList<OrganizationDomainView>))]
[JsonSerializable(typeof(InvitationBody))]
[JsonSerializable(typeof(IssuedInvitationView))]
[JsonSerializable(typeof(OrganizationSummaryView))]
[JsonSerializable(typeof(OrganizationPageView))]
[JsonSerializable(typeof(MemberView))]
[JsonSerializable(typeof(MemberPageView))]
[JsonSerializable(typeof(IReadOnlyList<StandingInvitationView>))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class OrganizationJson : JsonSerializerContext;
