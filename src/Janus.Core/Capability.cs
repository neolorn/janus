using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a caller may do to one record, subject to the session gates the per-row grant
/// query does not evaluate.
/// </summary>
/// <param name="Resource">The record.</param>
/// <param name="Can">The permissions the caller's grants confer on it.</param>
/// <param name="Requires">
/// What each of those still requires before the action will succeed, so that a control
/// is prompted for rather than hidden or shown and refused.
/// </param>
/// <remarks>
/// Implements AUTHZ-GATE-005 and API-CAP-001. A capability is permitted by grants, not
/// a promise the action will succeed: a permission absent from <paramref name="Requires"/>
/// has nothing outstanding.
/// </remarks>
public sealed record Capability(
    ResourceId Resource,
    IReadOnlySet<Permission> Can,
    IReadOnlyDictionary<Permission, IReadOnlySet<CapabilityResidual>> Requires);
