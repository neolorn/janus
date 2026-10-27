using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// One page of an organization's current members, as the response carries it.
/// </summary>
/// <param name="Members">The members, ordered by subject.</param>
/// <param name="Next">The subject the next page begins after, or nothing where none remain.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-READ-002 and API-CONV-002.</remarks>
internal sealed record MemberPageView(IReadOnlyList<MemberView> Members, Guid? Next)
{
    /// <summary>
    /// The view of one page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The page is absent.</exception>
    public static MemberPageView Of(MemberPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new([.. page.Members.Select(MemberView.Of)], page.Next?.Value);
    }
}
