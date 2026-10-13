using System;

namespace Janus.Storage;

/// <summary>
/// The instant a claim on a row carried out of the database stands until, as the row
/// carries it.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003. The instant names the claim: the attempt's outcome is
/// written only where the row still carries it. The database keeps microseconds, so the
/// instant is cut to them before it is written, and what is compared later is what was
/// stored.
/// </remarks>
internal static class RowClaim
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary>
    /// When a claim made at one instant times out.
    /// </summary>
    /// <param name="now">The instant of the claim.</param>
    /// <param name="timeout">How long it stands.</param>
    /// <returns>The instant, to the microsecond.</returns>
    public static DateTimeOffset Until(DateTimeOffset now, TimeSpan timeout)
    {
        long ticks = (now + timeout).UtcTicks;

        return new DateTimeOffset(ticks - (ticks % TicksPerMicrosecond), TimeSpan.Zero);
    }
}
