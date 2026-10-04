using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// One row of a declared relationship as the host's source answers it: who holds it,
/// and the record it is held on.
/// </summary>
/// <param name="Holder">The subject the row names.</param>
/// <param name="Resource">The record the row names, of the type the relationship is declared on.</param>
/// <remarks>Implements AUTHZ-DERIVE-007 and AUTHZ-DERIVE-001.</remarks>
internal sealed record HeldRelationship(SubjectId Holder, ResourceId Resource);
