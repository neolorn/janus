using System;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// What the trigger of a takedown answers with.
/// </summary>
/// <param name="TakedownId">What the takedown is held under.</param>
/// <param name="ErasureDue">When the grace window ends and the erasure runs.</param>
/// <remarks>Implements chapter 09 section 8a and IDN-LIFE-003.</remarks>
internal sealed record ExecutedTakedownView(Guid TakedownId, DateTimeOffset ErasureDue)
{
    /// <summary>
    /// The view of a takedown just triggered.
    /// </summary>
    /// <param name="takedown">The takedown.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The takedown is absent.</exception>
    public static ExecutedTakedownView Of(ExecutedTakedown takedown)
    {
        ArgumentNullException.ThrowIfNull(takedown);

        return new ExecutedTakedownView(takedown.Id.Value, takedown.ErasureDue);
    }
}
