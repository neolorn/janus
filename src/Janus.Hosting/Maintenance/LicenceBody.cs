using System;

namespace Janus.Hosting.Maintenance;

/// <summary>
/// One licence or permit as <c>PUT /admin/compliance/licences</c> carries it.
/// </summary>
/// <param name="Id">What it is known by, which the management application names.</param>
/// <param name="Kind"><c>licence</c> or <c>permit</c>.</param>
/// <param name="Name">What the operator calls it.</param>
/// <param name="ExpiresAt">When it lapses.</param>
/// <param name="RenewedAt">When it was last renewed, where it has been.</param>
/// <remarks>Implements OPS-MAINT-001 (D-153) and chapter 09 section 8a.</remarks>
internal sealed record LicenceBody(
    Guid? Id,
    string? Kind,
    string? Name,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RenewedAt);
