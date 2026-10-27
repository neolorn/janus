using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// Which accounts may exercise a permission on a record, and whether every derivation
/// reaching it was evaluated.
/// </summary>
/// <param name="Subjects">The accounts the single check would admit.</param>
/// <param name="Partial">Whether the bound stopped a derivation before it was evaluated.</param>
/// <param name="Unevaluated">The relationships of the derivations the bound stopped, by name.</param>
/// <remarks>Implements AUTHZ-GATE-007 and chapter 09 section 8a (D-218).</remarks>
internal sealed record AdmittedAccountsView(
    IReadOnlyList<Guid> Subjects,
    bool Partial,
    IReadOnlyList<string> Unevaluated)
{
    /// <summary>
    /// The view of which accounts may exercise a permission.
    /// </summary>
    /// <param name="admitted">The accounts.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The answer is absent.</exception>
    public static AdmittedAccountsView Of(AdmittedAccounts admitted)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        return new AdmittedAccountsView(
            [.. admitted.Subjects.Select(subject => subject.Value)],
            admitted.Partial,
            admitted.Unevaluated);
    }
}
