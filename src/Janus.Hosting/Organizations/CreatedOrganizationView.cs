using System;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// The organization just created, as the response carries it.
/// </summary>
/// <param name="Id">Its identifier, which every other organization route names.</param>
/// <remarks>Implements chapter 09 section 8a and API-CONV-002.</remarks>
internal sealed record CreatedOrganizationView(Guid Id)
{
    /// <summary>
    /// The view of one organization's identifier.
    /// </summary>
    /// <param name="organization">The identifier.</param>
    /// <returns>The view.</returns>
    public static CreatedOrganizationView Of(OrganizationId organization) => new(organization.Value);
}
