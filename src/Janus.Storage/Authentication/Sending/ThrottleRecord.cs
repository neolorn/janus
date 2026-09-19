using System;
using Janus.Authentication.Sending;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>throttle_counters</c> row: what one scope has accumulated.
/// </summary>
/// <remarks>
/// Implements AUTH-ABUSE-001. The source, the account and the identifier are held by
/// their hash, so an identifier no account holds leaves no readable trace of having
/// been typed.
/// </remarks>
internal sealed class ThrottleRecord
{
    /// <summary>The <c>scope</c> column.</summary>
    public ThrottleScope Scope { get; set; }

    /// <summary>The <c>key</c> column.</summary>
    public byte[] Key { get; set; } = [];

    /// <summary>The <c>failures</c> column.</summary>
    public int Failures { get; set; }

    /// <summary>The <c>at</c> column.</summary>
    public DateTimeOffset At { get; set; }
}
