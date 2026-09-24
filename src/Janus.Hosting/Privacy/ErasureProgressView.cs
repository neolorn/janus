using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where an erasure's host-side work has got.
/// </summary>
/// <param name="Id">What the erasure is held under.</param>
/// <param name="Subject">Whose erasure it is.</param>
/// <param name="Reason">Why it ran.</param>
/// <param name="Status">How far the subscribers have got.</param>
/// <param name="Attempts">How many delivery attempts have been made across them.</param>
/// <param name="Subscribers">Each registered subscriber and whether it confirmed.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-LIFE-003a and IDN-LIFE-003b.</remarks>
internal sealed record ErasureProgressView(
    Guid Id,
    Guid Subject,
    ErasureReason Reason,
    ErasureStatus Status,
    int Attempts,
    IReadOnlyList<SubscriberConfirmationView> Subscribers)
{
    /// <summary>
    /// The view of an erasure's progress.
    /// </summary>
    /// <param name="progress">The progress.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The progress is absent.</exception>
    public static ErasureProgressView Of(ErasureProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new ErasureProgressView(
            progress.Id.Value,
            progress.Subject.Value,
            progress.Reason,
            progress.Status,
            progress.Attempts,
            [.. progress.Subscribers.Select(SubscriberConfirmationView.Of)]);
    }
}
