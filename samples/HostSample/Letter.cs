using System;

namespace HostSample;

/// <summary>
/// A message the library handed the workshop, waiting in the post room for the courier,
/// under the reference the library gave it.
/// </summary>
internal sealed class Letter
{
    /// <summary>
    /// The reference the library gave the message, the same at every hand-over.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// What the message is for, which the courier's wording follows.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Whether it goes by mail or by text.
    /// </summary>
    public required string Channel { get; init; }

    /// <summary>
    /// The address or number it goes to.
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// The languages it is written in, in order, separated by commas.
    /// </summary>
    public required string Languages { get; init; }

    /// <summary>
    /// When the post room took it.
    /// </summary>
    public required DateTimeOffset TakenAt { get; init; }
}
