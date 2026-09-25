using System;
using Janus.Core;

namespace Janus.Hosting.Authorization;

/// <summary>
/// The group just created, as the response carries it.
/// </summary>
/// <param name="Id">Its identifier, which a grant and a change of members name.</param>
/// <remarks>Implements chapter 09 section 8a and API-CONV-002.</remarks>
internal sealed record CreatedGroupView(Guid Id)
{
    /// <summary>
    /// The view of one group's identifier.
    /// </summary>
    /// <param name="group">The identifier.</param>
    /// <returns>The view.</returns>
    public static CreatedGroupView Of(GroupId group) => new(group.Value);
}
