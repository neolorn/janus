using System;

namespace Janus.Core;

/// <summary>
/// One browser as the account lists it.
/// </summary>
/// <param name="Id">Which browser, as the removal names it.</param>
/// <param name="Kind">What it is known for.</param>
/// <param name="Label">What the person calls it.</param>
/// <param name="CreatedAt">When it became known.</param>
/// <param name="LastUsedAt">When it was last seen.</param>
/// <remarks>
/// Implements chapter 9 section 6, AUTH-FACT-015 and AUTH-FACT-016. Sessions are not
/// listed here; they are their own list (AUTH-SESS-013).
/// </remarks>
public sealed record DeviceSummary(
    DeviceId Id,
    DeviceKind Kind,
    CredentialLabel Label,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastUsedAt);
