using System;
using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// One licence or permit as <c>PUT /admin/compliance/licences</c> carries it.
/// </summary>
/// <param name="Id">What it is known by, which the management application names.</param>
/// <param name="Kind"><c>licence</c> or <c>permit</c>.</param>
/// <param name="Name">What the operator calls it.</param>
/// <param name="ExpiresAt">When it lapses.</param>
/// <param name="RenewedAt">When it was last renewed, where it has been.</param>
/// <remarks>
/// Implements OPS-MAINT-001 (D-153) and chapter 09 section 8a. The body of the route is
/// the list of these itself, so a member is named by its own name (API-CONV-002 AC4).
/// </remarks>
internal sealed record LicenceBody(
    Guid? Id,
    string? Kind,
    string? Name,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RenewedAt)
{
    /// <summary>
    /// The licences a body describes, or the member it cannot be read at.
    /// </summary>
    /// <param name="body">The list the request carries.</param>
    /// <returns>The licences, or nothing and the member that stopped them.</returns>
    /// <exception cref="ArgumentNullException">The list is absent.</exception>
    public static (IReadOnlyList<Licence>? Licences, string Member) Read(IReadOnlyList<LicenceBody?> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var read = new List<Licence>(body.Count);

        foreach (LicenceBody? licence in body)
        {
            if (licence?.Id is not { } id)
            {
                return (null, "id");
            }

            LicenceKind? kind = licence.Kind switch
            {
                "licence" => LicenceKind.Licence,
                "permit" => LicenceKind.Permit,
                _ => null,
            };

            if (kind is not LicenceKind named)
            {
                return (null, "kind");
            }

            if (string.IsNullOrWhiteSpace(licence.Name))
            {
                return (null, "name");
            }

            if (licence.ExpiresAt is not { } expiresAt)
            {
                return (null, "expiresAt");
            }

            read.Add(new Licence(new LicenceId(id), named, licence.Name, expiresAt, licence.RenewedAt));
        }

        return (read, string.Empty);
    }
}
