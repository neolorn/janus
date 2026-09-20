using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>callbacks</c> row: one inbound callback and whether it was rejected.
/// </summary>
/// <remarks>
/// Implements INT-GEN-003 and BFF-MACH-003. The source is held by its hash: the row
/// exists to count, not to record who called.
/// </remarks>
internal sealed class CallbackRecord
{
    /// <summary>The <c>id</c> column, which is this table key.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>source</c> column.</summary>
    public byte[] Source { get; set; } = [];

    /// <summary>The <c>at</c> column.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The <c>rejected</c> column.</summary>
    public bool Rejected { get; set; }
}
