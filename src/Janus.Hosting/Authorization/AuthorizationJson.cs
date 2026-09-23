using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Janus.Hosting.Authorization;

/// <summary>
/// How the administration of access reads and writes, generated rather than reflected
/// over (CONV-CODE-004, CONV-DESIGN-006).
/// </summary>
/// <remarks>Implements AUTHZ-GATE-004, AUTHZ-GRANT-001, AUTHZ-GRANT-003, AUTHZ-GRANT-004, AUTHZ-GROUP-001 and API-CONV-002.</remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CreatedGrantView))]
[JsonSerializable(typeof(CreatedGroupView))]
[JsonSerializable(typeof(ExplanationView))]
[JsonSerializable(typeof(GrantBody))]
[JsonSerializable(typeof(GrantRevocationBody))]
[JsonSerializable(typeof(GroupBody))]
[JsonSerializable(typeof(GroupRemovalBody))]
[JsonSerializable(typeof(GroupView))]
[JsonSerializable(typeof(IReadOnlyList<GroupView>))]
[JsonSerializable(typeof(MemberBody))]
[JsonSerializable(typeof(RoleBody))]
[JsonSerializable(typeof(RoleRemovalBody))]
[JsonSerializable(typeof(RoleView))]
[JsonSerializable(typeof(IReadOnlyList<RoleView>))]
internal sealed partial class AuthorizationJson : JsonSerializerContext;
