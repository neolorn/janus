using System;

namespace HostSample;

/// <summary>
/// A repair as the people who may read it see it.
/// </summary>
/// <param name="Ticket">The ticket on the kite's tag.</param>
/// <param name="Workshop">The workshop the kite is at.</param>
/// <param name="Kite">What the kite is.</param>
/// <param name="ContactName">The name to contact the customer by, or nothing where the customer's key is erased.</param>
/// <param name="ContactErased">Whether the customer's key is erased, so the name no longer decrypts.</param>
/// <param name="FollowUp">Whether the workshop calls the customer after the repair.</param>
/// <param name="ReleasedAt">When the kite was released, or nothing while it is in the workshop.</param>
public sealed record RepairView(
    string Ticket,
    string Workshop,
    string Kite,
    string? ContactName,
    bool ContactErased,
    bool FollowUp,
    DateTimeOffset? ReleasedAt);
