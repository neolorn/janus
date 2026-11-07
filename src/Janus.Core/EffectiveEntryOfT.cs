using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// One permission or one role a role carries or hands out once its inclusions are
/// followed, with where it came from.
/// </summary>
/// <typeparam name="T">A permission, or a role name.</typeparam>
/// <param name="Name">The permission or the role.</param>
/// <param name="From">
/// The roles it came through, nearest first, and none where the role carries it itself.
/// Where it comes several ways, the role's own wins, then the shortest path, then among
/// paths of one length the first by the ordinal order of the role names along it.
/// </param>
/// <remarks>Implements AUTHZ-GRANT-006 (D-278).</remarks>
public sealed record EffectiveEntry<T>(T Name, IReadOnlyList<RoleName> From);
