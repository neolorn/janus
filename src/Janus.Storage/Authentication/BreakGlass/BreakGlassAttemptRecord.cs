using System;

namespace Janus.Storage.Authentication.BreakGlass;

/// <summary>
/// The <c>break_glass_attempts</c> row: one attempt at the credential, from any source.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-004 AC7. The row holds when the attempt arrived and nothing about
/// who made it or what it presented.
/// </remarks>
internal sealed class BreakGlassAttemptRecord
{
    /// <summary>The <c>id</c> column, this table key.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>attempted_at</c> column.</summary>
    public DateTimeOffset AttemptedAt { get; set; }
}
