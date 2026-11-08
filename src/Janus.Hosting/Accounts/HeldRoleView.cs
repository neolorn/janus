using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;
using Janus.Hosting.Authorization;

namespace Janus.Hosting.Accounts;

/// <summary>
/// One role a person holds themselves, as their own account's surface reads it.
/// </summary>
/// <param name="Name">The role.</param>
/// <param name="Effective">What it carries and hands out once its inclusions are followed.</param>
/// <param name="GivenBy">The roles whose effective <c>handsOut</c> names it.</param>
/// <remarks>Implements chapter 09 <c>GET /account/roles</c> and AUTHZ-GRANT-006 (D-280).</remarks>
internal sealed record HeldRoleView(string Name, EffectiveRoleView Effective, IReadOnlyList<string> GivenBy)
{
    /// <summary>
    /// The view of one held role.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The role is absent.</exception>
    public static HeldRoleView Of(HeldRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        return new HeldRoleView(
            role.Name.ToString(),
            EffectiveRoleView.Of(role.Effective),
            [.. role.GivenBy.Select(giving => giving.ToString())]);
    }
}
