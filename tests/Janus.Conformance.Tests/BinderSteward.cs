using Janus.Core;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's fact that a person looks after a binder, which the steward role
/// is derived from.
/// </summary>
public sealed class BinderSteward
{
    /// <summary>
    /// The binder looked after.
    /// </summary>
    public required string BinderId { get; init; }

    /// <summary>
    /// Who looks after it.
    /// </summary>
    public required SubjectId Steward { get; init; }
}
