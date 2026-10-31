using System;

namespace HostSample;

/// <summary>
/// An alert the library raised, pinned on the workshop's duty board for whoever is on
/// duty, under the key the library raised it with.
/// </summary>
internal sealed class PinnedAlert
{
    /// <summary>
    /// The key the library raised the alert with, the same at every hand-over.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The condition raised.
    /// </summary>
    public required string Condition { get; init; }

    /// <summary>
    /// How urgent it is.
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// The one of several things it was raised for, where the condition names one.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// When the library raised it.
    /// </summary>
    public required DateTimeOffset RaisedAt { get; init; }
}
