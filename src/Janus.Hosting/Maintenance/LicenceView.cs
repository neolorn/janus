using System;
using Janus.Authentication;
using Janus.Authentication.Maintenance;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// One licence or permit as the response carries it.
/// </summary>
/// <param name="Id">What it is known by.</param>
/// <param name="Kind"><c>licence</c> or <c>permit</c>.</param>
/// <param name="Name">What the operator calls it.</param>
/// <param name="ExpiresAt">When it lapses.</param>
/// <param name="RenewedAt">When it was last renewed, where it has been.</param>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
internal sealed record LicenceView(
    Guid Id,
    string Kind,
    string Name,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RenewedAt)
{
    /// <summary>
    /// The view of one licence or permit.
    /// </summary>
    /// <param name="licence">The licence or permit.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The licence is absent.</exception>
    public static LicenceView Of(Licence licence)
    {
        ArgumentNullException.ThrowIfNull(licence);

        return new(
            licence.Id.Value,
            WrittenName.Of(licence.Kind),
            licence.Name,
            licence.ExpiresAt,
            licence.RenewedAt);
    }
}
