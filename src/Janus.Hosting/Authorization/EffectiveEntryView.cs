using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One permission or role a role carries or hands out, with where it came from.
/// </summary>
/// <param name="Name">The permission or the role.</param>
/// <param name="From">
/// The roles it came through, nearest first, and none where the role carries it itself.
/// </param>
/// <remarks>Implements chapter 09 section 8 and AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed record EffectiveEntryView(string Name, IReadOnlyList<string> From)
{
    /// <summary>
    /// The view of one entry.
    /// </summary>
    /// <param name="name">The permission or the role, as written.</param>
    /// <param name="from">The roles it came through, nearest first.</param>
    /// <returns>The view.</returns>
    public static EffectiveEntryView Of(string name, IReadOnlyList<RoleName> from) =>
        new(name, [.. from.Select(role => role.ToString())]);
}
