using System;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// The <c>sms_balance_readings</c> row: what the gateway said its account stood at.
/// </summary>
/// <remarks>Implements INT-SMS-004 and AUTH-ABUSE-006.</remarks>
internal sealed class BalanceReadingRecord
{
    /// <summary>The <c>read_at</c> column, which is this table key.</summary>
    public DateTimeOffset ReadAt { get; set; }

    /// <summary>The <c>balance</c> column.</summary>
    public decimal Balance { get; set; }
}
