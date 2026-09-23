using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// One group as the management application reads it.
/// </summary>
/// <param name="Id">The group's identifier.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Members">The accounts and groups it holds directly.</param>
/// <remarks>Implements chapter 09 section 8a and AUTHZ-GROUP-001.</remarks>
internal sealed record GroupView(Guid Id, string Name, IReadOnlyList<MemberView> Members)
{
    /// <summary>
    /// The view of one group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The group is absent.</exception>
    public static GroupView Of(DefinedGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new GroupView(
            group.Id.Value,
            group.Name,
            [.. group.Members.Select(member => new MemberView(member.Type, member.Value))]);
    }
}
