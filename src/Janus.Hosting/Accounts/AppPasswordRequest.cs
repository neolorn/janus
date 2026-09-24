using System;

namespace Janus.Hosting.Accounts;

/// <summary>
/// What a person asks the mail server to generate an app password with.
/// </summary>
/// <param name="Label">What they call it.</param>
/// <param name="ExpiresAt">When it stops working, or nothing for no expiry.</param>
/// <remarks>Implements REG-MAIL-002.</remarks>
internal sealed record AppPasswordRequest(string? Label, DateTimeOffset? ExpiresAt);
