using System.Collections.Generic;
using Janus.Core;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// The body of <c>PUT /admin/compliance/licences</c>: every licence and permit that
/// now stands.
/// </summary>
/// <param name="Licences">The licences and permits.</param>
/// <remarks>Implements OPS-MAINT-001 and chapter 09 section 8a.</remarks>
internal sealed record LicencesBody(IReadOnlyList<LicenceBody?>? Licences)
{
    /// <summary>
    /// The licences the body describes, or the member it cannot be read at.
    /// </summary>
    /// <returns>The licences, or nothing and the member that stopped them.</returns>
    public (IReadOnlyList<Licence>? Licences, string Member) Read()
    {
        if (Licences is null)
        {
            return (null, "licences");
        }

        var read = new List<Licence>(Licences.Count);

        foreach (LicenceBody? licence in Licences)
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
