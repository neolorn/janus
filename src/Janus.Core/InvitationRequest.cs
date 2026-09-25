using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What an invitation into an organization names.
/// </summary>
/// <param name="Email">
/// The email the invitation binds and its link goes to, or nothing to leave the email
/// open. Where the organization's mail is integrated it is the person's personal email
/// and is required.
/// </param>
/// <param name="Phone">The phone the invitation binds, or nothing to leave it open.</param>
/// <param name="CorporateEmail">
/// The corporate address the administrator asserts, where the organization's mail is
/// integrated and only there.
/// </param>
/// <param name="Roles">The roles granted across the organization when the membership attaches.</param>
/// <param name="Documents">
/// The documents the person is shown at the membership step, each at the version
/// current when the invitation is issued.
/// </param>
/// <remarks>Implements REG-INV-001, REG-MAIL-001 and IDN-LIFE-009a.</remarks>
public sealed record InvitationRequest(
    string? Email,
    string? Phone,
    string? CorporateEmail,
    IReadOnlyList<RoleName> Roles,
    IReadOnlyList<string> Documents);
