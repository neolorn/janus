using System;
using System.Collections.Frozen;

namespace Janus.Core;

/// <summary>
/// The names of the step-up actions the library once had and removed, kept beside the
/// current ones, so that a gate a stored policy still names for one is told apart from
/// a damaged row.
/// </summary>
/// <remarks>Implements chapter 10 section 5a (D-235).</remarks>
internal static class RetiredStepUpActions
{
    /// <summary>
    /// Gets the retired names: <c>alerting:destinations</c>, since alert destinations
    /// are the host's (D-225).
    /// </summary>
    public static FrozenSet<string> Names { get; } =
        new[] { "alerting:destinations" }.ToFrozenSet(StringComparer.Ordinal);
}
