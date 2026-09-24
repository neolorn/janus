using System;

namespace Janus.Core;

/// <summary>
/// One mail app password as the mail server's listing describes it. The secret is not
/// here: the server shows it once, when it generates it, and never again.
/// </summary>
/// <param name="Id">What the server calls it, which is what a revocation names.</param>
/// <param name="Label">What the person called it.</param>
/// <param name="CreatedAt">When the server generated it.</param>
/// <param name="ExpiresAt">When it stops working, where one was set.</param>
/// <remarks>Implements REG-MAIL-002 and INT-MAIL-010 AC2.</remarks>
public sealed record AppPassword(
    string Id,
    string Label,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);
