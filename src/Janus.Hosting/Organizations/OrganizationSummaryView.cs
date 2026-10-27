using System;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// One organization, as a read of it and the list carry it.
/// </summary>
/// <param name="Id">Its identifier.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Administrative">Whether it administers the deployment.</param>
/// <param name="DeletionRequestedAt">When its deletion was requested, or nothing.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-READ-001 and API-CONV-002.</remarks>
internal sealed record OrganizationSummaryView(
    Guid Id,
    string Name,
    bool Administrative,
    DateTimeOffset? DeletionRequestedAt)
{
    /// <summary>
    /// The view of one organization.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The organization is absent.</exception>
    public static OrganizationSummaryView Of(OrganizationSummary organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new(organization.Id.Value, organization.Name, organization.Administrative, organization.DeletionRequestedAt);
    }
}
