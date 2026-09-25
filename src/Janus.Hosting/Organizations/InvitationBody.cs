using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Organizations;

/// <summary>
/// What issuing an invitation carries, as the request reads.
/// </summary>
/// <param name="Email">
/// The email the invitation binds and its link goes to, the personal email where the
/// organization's mail is integrated; absent to leave the email open.
/// </param>
/// <param name="Phone">The phone the invitation binds; absent to leave it open.</param>
/// <param name="CorporateEmail">
/// The corporate address the administrator asserts, where the organization's mail is
/// integrated.
/// </param>
/// <param name="Roles">The roles granted across the organization with the membership; none where absent.</param>
/// <param name="Documents">The documents shown at the membership step; none where absent.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-LIFE-009a, REG-INV-001 and REG-MAIL-001.</remarks>
internal sealed record InvitationBody(
    string? Email,
    string? Phone,
    string? CorporateEmail,
    IReadOnlyList<string>? Roles,
    IReadOnlyList<string>? Documents)
{
    /// <summary>
    /// The invitation the body describes, or the member it cannot be read at.
    /// </summary>
    /// <returns>The invitation, or nothing and the member that stopped it.</returns>
    public (InvitationRequest? Request, string Member) Read()
    {
        var roles = new List<RoleName>(Roles?.Count ?? 0);

        foreach (string? named in Roles ?? [])
        {
            if (!RoleName.TryParse(named, out RoleName role))
            {
                return (null, "roles");
            }

            roles.Add(role);
        }

        return (new InvitationRequest(Email, Phone, CorporateEmail, roles, Documents ?? []), string.Empty);
    }
}
