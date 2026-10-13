using System;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// The <c>callback_events</c> row: one provider event a callback was claimed for.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-002 and INT-GEN-003. The identifier is held by its hash, which is
/// all a repeated delivery has to be matched against.
/// </remarks>
internal sealed class CallbackEventRecord
{
    /// <summary>The <c>callback</c> column, part of this table key.</summary>
    public string Callback { get; set; } = string.Empty;

    /// <summary>The <c>identifier</c> column, part of this table key.</summary>
    public byte[] Identifier { get; set; } = [];

    /// <summary>The <c>claimed_at</c> column.</summary>
    public DateTimeOffset ClaimedAt { get; set; }

    /// <summary>
    /// The <c>settled_at</c> column: when the event was carried, or nothing while a
    /// delivery of it is still being carried.
    /// </summary>
    public DateTimeOffset? SettledAt { get; set; }
}
