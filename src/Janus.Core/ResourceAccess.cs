using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// Who can access one record, and through which grant or container: the stored and
/// materialised grants on it, on what contains it and on its whole organization, and
/// the grants its declared derivations produce.
/// </summary>
/// <param name="Resource">The record in question.</param>
/// <param name="Grants">
/// Each grant reaching the record, its kind saying whether somebody wrote it, a
/// materialisation wrote it, or a fact in the host's data produced it.
/// </param>
/// <param name="Partial">
/// Whether evaluation stopped at <c>authz.reverselookup.budget</c>, so that the grants
/// the derivations named in <paramref name="Unevaluated"/> would produce are missing.
/// </param>
/// <param name="Unevaluated">The derivations not evaluated, by the relationship each follows from.</param>
/// <remarks>
/// Implements AUTHZ-DERIVE-007, AUTHZ-GATE-004 and chapter 09 section 8. Stored and
/// derived grants are told apart by their kind, and a limitation is stated rather than
/// left for the reader to miss.
/// </remarks>
public sealed record ResourceAccess(
    ResourceReference Resource,
    IReadOnlyList<ExplainedGrant> Grants,
    bool Partial,
    IReadOnlyList<string> Unevaluated);
