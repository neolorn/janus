using System;
using System.Collections.Generic;

namespace Janus.Authentication.Sending;

/// <summary>
/// What the ledger knows about one restriction key: when it was sent to, and how much
/// credit support has added to it.
/// </summary>
/// <param name="Sends">The times counted against the key, oldest first.</param>
/// <param name="Credit">The unspent grants, zero where none was made.</param>
/// <remarks>Implements AUTH-ABUSE-004.</remarks>
internal sealed record SendCounter(IReadOnlyList<DateTimeOffset> Sends, int Credit)
{
    /// <summary>
    /// The key nothing has been counted against.
    /// </summary>
    public static SendCounter Empty { get; } = new([], 0);
}
