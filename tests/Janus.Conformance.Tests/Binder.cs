namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's kind of thing kept on a shelf.
/// </summary>
public sealed class Binder
{
    /// <summary>
    /// The binder's identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The shelf it is kept on.
    /// </summary>
    public required string ShelfId { get; init; }
}
