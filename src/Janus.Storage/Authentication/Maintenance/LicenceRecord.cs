using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Maintenance;

/// <summary>
/// The <c>licences</c> row: one licence or permit whose expiry the system warns of.
/// </summary>
/// <remarks>Implements OPS-MAINT-001 (D-153).</remarks>
internal sealed class LicenceRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public LicenceId Id { get; set; }

    /// <summary>The <c>kind</c> column: <c>licence</c> or <c>permit</c>.</summary>
    public LicenceKind Kind { get; set; }

    /// <summary>The <c>name</c> column.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The <c>expires_at</c> column.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>renewed_at</c> column, absent where it has not been renewed.</summary>
    public DateTimeOffset? RenewedAt { get; set; }
}
