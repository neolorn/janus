using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Who can access one record, and whether every derivation reaching it was evaluated.
/// </summary>
/// <param name="Resource">The record.</param>
/// <param name="Grants">Every grant reaching it, stored and derived told apart by their kind.</param>
/// <param name="Partial">Whether the bound stopped a derivation before it was evaluated.</param>
/// <param name="Unevaluated">The relationships of the derivations the bound stopped, by name.</param>
/// <remarks>Implements AUTHZ-DERIVE-007 and AUTHZ-GATE-004 (entry 265).</remarks>
internal sealed record ResourceAccessView(
    ResourceView Resource,
    IReadOnlyList<ExplainedGrantView> Grants,
    bool Partial,
    IReadOnlyList<string> Unevaluated)
{
    /// <summary>
    /// The view of who can access a record.
    /// </summary>
    /// <param name="access">Who can access it.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The answer is absent.</exception>
    public static ResourceAccessView Of(ResourceAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        return new ResourceAccessView(
            ResourceView.Of(access.Resource),
            [.. access.Grants.Select(ExplainedGrantView.Of)],
            access.Partial,
            access.Unevaluated);
    }
}
