using System;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// The <c>callback_references</c> row: one correlation reference issued for a host's
/// unsigned callback.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-003 and INT-GEN-003. The reference is held by its hash, so a
/// copy of the table yields none a callback could be forged with.
/// </remarks>
internal sealed class CallbackReferenceRecord
{
    /// <summary>The <c>reference</c> column, which is this table key.</summary>
    public byte[] Reference { get; set; } = [];

    /// <summary>The <c>callback</c> column.</summary>
    public string Callback { get; set; } = string.Empty;

    /// <summary>The <c>issued_at</c> column.</summary>
    public DateTimeOffset IssuedAt { get; set; }
}
