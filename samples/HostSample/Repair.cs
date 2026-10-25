using System;
using Janus.Core;

namespace HostSample;

/// <summary>
/// A kite taken in for repair, under the ticket its tag carries, with the name of the
/// customer to contact held under the customer's key.
/// </summary>
internal sealed class Repair
{
    /// <summary>
    /// The ticket on the kite's tag, under which the repair is registered with the
    /// library.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The workshop the kite was taken in at.
    /// </summary>
    public required string WorkshopId { get; init; }

    /// <summary>
    /// The customer who brought the kite in, whose key the contact name is held under.
    /// </summary>
    public required SubjectId Customer { get; init; }

    /// <summary>
    /// What the kite is, as the customer describes it.
    /// </summary>
    public required string Kite { get; init; }

    /// <summary>
    /// The name to contact the customer by, encrypted under the customer's key.
    /// </summary>
    public required byte[] ContactName { get; init; }

    /// <summary>
    /// Whether the workshop calls the customer after the repair to ask how the kite
    /// flies.
    /// </summary>
    public required bool FollowUp { get; init; }

    /// <summary>
    /// When the repaired kite was released to the customer, or nothing while it is in
    /// the workshop.
    /// </summary>
    public DateTimeOffset? ReleasedAt { get; init; }
}
