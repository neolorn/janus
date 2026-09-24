using System;
using Janus.Authentication.BreakGlass;
using Janus.Core;

namespace Janus.Storage.Authentication.BreakGlass;

/// <summary>
/// The <c>break_glass_credentials</c> row: one issue of the break-glass credential.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-004 and CONV-DESIGN-003. The code is held as a password is:
/// verifiable and never recoverable.
/// </remarks>
internal sealed class BreakGlassCredentialRecord
{
    /// <summary>The <c>id</c> column, this table key.</summary>
    public BreakGlassCredentialId Id { get; set; }

    /// <summary>The <c>hash</c> column.</summary>
    [NeverLogged]
    public string Hash { get; set; } = string.Empty;

    /// <summary>The <c>issued_by</c> column.</summary>
    public SubjectId IssuedBy { get; set; }

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>The <c>consumed_at</c> column, unset while the issue is unspent.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>The <c>replaced_at</c> column, unset while no later issue replaced it.</summary>
    public DateTimeOffset? ReplacedAt { get; set; }
}
