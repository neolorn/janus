using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// One page of the organizations a caller may read, as the response carries it.
/// </summary>
/// <param name="Organizations">The organizations, ordered by identifier.</param>
/// <param name="Next">The identifier the next page begins after, or nothing where none remain.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-READ-001 and API-CONV-002.</remarks>
internal sealed record OrganizationPageView(IReadOnlyList<OrganizationSummaryView> Organizations, Guid? Next)
{
    /// <summary>
    /// The view of one page.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The page is absent.</exception>
    public static OrganizationPageView Of(OrganizationPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new([.. page.Organizations.Select(OrganizationSummaryView.Of)], page.Next?.Value);
    }
}
