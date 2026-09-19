using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The <c>devices</c> row.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-015, AUTH-FACT-016 and CONV-DESIGN-003. The token the browser
/// carries is held by its fingerprint, so a dump yields nothing a browser could
/// present.
/// </remarks>
internal sealed class DeviceRecord
{
    /// <summary>The <c>id</c> column, which is this table key.</summary>
    public DeviceId Id { get; set; }

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>kind</c> column.</summary>
    public DeviceKind Kind { get; set; }

    /// <summary>The <c>label</c> column.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The <c>token_fingerprint</c> column.</summary>
    public byte[] TokenFingerprint { get; set; } = [];

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>last_used_at</c> column.</summary>
    public DateTimeOffset LastUsedAt { get; set; }

    /// <summary>The <c>expires_at</c> column.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>consecutive_failures</c> column.</summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>The <c>revoked</c> column.</summary>
    public bool Revoked { get; set; }
}
