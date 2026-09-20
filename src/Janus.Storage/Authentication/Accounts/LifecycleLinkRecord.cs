using System;
using Janus.Authentication.Accounts;
using Janus.Core;

namespace Janus.Storage.Authentication.Accounts;

/// <summary>
/// The <c>lifecycle_links</c> row: the one link an account's own deactivation or
/// deletion notice carried.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-013 and IDN-LIFE-014. The token is held by its fingerprint,
/// so a database dump yields nothing that can stand an account back up.
/// </remarks>
internal sealed class LifecycleLinkRecord
{
    /// <summary>The <c>token</c> column: what the link's token hashes to.</summary>
    public byte[] Token { get; set; } = [];

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>kind</c> column: which state the link ends.</summary>
    public LifecycleLinkKind Kind { get; set; }

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }
}
