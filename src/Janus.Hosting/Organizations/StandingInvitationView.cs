using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// One standing invitation into an organization, as the response carries it.
/// </summary>
/// <param name="Id">Its identifier.</param>
/// <param name="Roles">The roles it names.</param>
/// <param name="IssuedAt">When it was issued.</param>
/// <param name="ExpiresAt">When it can no longer be acknowledged.</param>
/// <param name="Issuer">Who issued it.</param>
/// <param name="Email">The email it binds, or nothing.</param>
/// <param name="Phone">The phone it binds, or nothing.</param>
/// <param name="CorporateAddress">The corporate address it binds, or nothing.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-READ-002 and API-CONV-002.</remarks>
internal sealed record StandingInvitationView(
    Guid Id,
    IReadOnlyList<string> Roles,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    Guid Issuer,
    string? Email,
    string? Phone,
    string? CorporateAddress)
{
    /// <summary>
    /// The view of one invitation.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The invitation is absent.</exception>
    public static StandingInvitationView Of(StandingInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return new(
            invitation.Id.Value,
            [.. invitation.Roles.Select(role => role.ToString())],
            invitation.IssuedAt,
            invitation.ExpiresAt,
            invitation.Issuer.Value,
            invitation.Email,
            invitation.Phone,
            invitation.CorporateAddress);
    }
}
