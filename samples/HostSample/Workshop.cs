using System;

namespace HostSample;

/// <summary>
/// A kite-repair workshop, which belongs to the organization that runs it and takes
/// kites in for repair up to its capacity.
/// </summary>
internal sealed class Workshop
{
    /// <summary>
    /// The workshop's identifier, under which it is registered with the library.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The organization that runs the workshop.
    /// </summary>
    public required Guid Organization { get; init; }

    /// <summary>
    /// The workshop's name, as its catalogue shows it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// How many kites it holds for repair at once.
    /// </summary>
    public required int Capacity { get; init; }
}
