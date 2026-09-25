namespace Janus.Conformance.Tests;

/// <summary>
/// A kind of thing a host maps and has declared no policy for.
/// </summary>
public sealed class Label
{
    /// <summary>
    /// The label's identifier.
    /// </summary>
    public required string Id { get; init; }
}
