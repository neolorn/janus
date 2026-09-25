using System;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's outermost kind of thing, owned by an organization.
/// </summary>
public sealed class Shelf
{
    /// <summary>
    /// The shelf's identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The organization owning it.
    /// </summary>
    public required Guid Organization { get; init; }
}
