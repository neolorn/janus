namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's innermost kind of thing, filed in a binder.
/// </summary>
public sealed class Sheet
{
    /// <summary>
    /// The sheet's identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The binder it is filed in.
    /// </summary>
    public required string BinderId { get; init; }
}
