using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's fact that a person keeps a shelf, which the keeper role is
/// derived from.
/// </summary>
public sealed class ShelfKeeper
{
    /// <summary>
    /// The shelf kept.
    /// </summary>
    public required string ShelfId { get; init; }

    /// <summary>
    /// Who keeps it.
    /// </summary>
    public required SubjectId Keeper { get; init; }
}
