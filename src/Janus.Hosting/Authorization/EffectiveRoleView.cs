using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// What a role carries and hands out once its inclusions are followed, each entry with its
/// source, as the management application reads it.
/// </summary>
/// <param name="Permissions">Its effective permissions.</param>
/// <param name="HandsOut">Its effective <c>handsOut</c>.</param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed record EffectiveRoleView(
    IReadOnlyList<EffectiveEntryView> Permissions,
    IReadOnlyList<EffectiveEntryView> HandsOut)
{
    /// <summary>
    /// The view of what a role carries and hands out.
    /// </summary>
    /// <param name="effective">What it carries and hands out.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">What it carries is absent.</exception>
    public static EffectiveRoleView Of(EffectiveRole effective)
    {
        ArgumentNullException.ThrowIfNull(effective);

        return new EffectiveRoleView(
            [.. effective.Permissions.Select(entry => EffectiveEntryView.Of(entry.Name.ToString(), entry.From))],
            [.. effective.HandsOut.Select(entry => EffectiveEntryView.Of(entry.Name.ToString(), entry.From))]);
    }
}
