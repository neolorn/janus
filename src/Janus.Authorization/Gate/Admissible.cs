using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authorization.Gate;

/// <summary>
/// The accounts that could be admitted for one permission, before the single check
/// judges each of them.
/// </summary>
/// <param name="Holding">The accounts an allowing grant names, directly or through a group.</param>
/// <param name="Deriving">The accounts a derivation conferring the permission names.</param>
/// <param name="Unevaluated">The derivations the bound stopped, by the relationship each follows from.</param>
/// <remarks>Implements AUTHZ-GATE-007 (D-218).</remarks>
internal sealed record Admissible(
    IReadOnlySet<SubjectId> Holding,
    IReadOnlySet<SubjectId> Deriving,
    IReadOnlyList<string> Unevaluated);
