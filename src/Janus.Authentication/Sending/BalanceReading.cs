using System;

namespace Janus.Authentication.Sending;

/// <summary>
/// What the gateway said its account stood at, and when it said it.
/// </summary>
/// <param name="At">When the balance was read.</param>
/// <param name="Balance">What it stood at, in the currency the gateway reports.</param>
/// <remarks>Implements INT-SMS-004 and AUTH-ABUSE-006.</remarks>
internal sealed record BalanceReading(DateTimeOffset At, decimal Balance);
