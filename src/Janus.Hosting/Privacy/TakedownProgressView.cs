using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where a takedown has got.
/// </summary>
/// <param name="TakedownId">What the takedown is held under.</param>
/// <param name="Subject">Whose account was taken down.</param>
/// <param name="TriggeredAt">When phase one committed.</param>
/// <param name="ErasureDue">When the grace window ends and the erasure runs.</param>
/// <param name="Status">How far the subscribers have got.</param>
/// <param name="Attempts">How many delivery attempts have been made across them.</param>
/// <param name="Subscribers">Each registered subscriber and whether it confirmed.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-LIFE-003 and IDN-LIFE-003a.</remarks>
internal sealed record TakedownProgressView(
    Guid TakedownId,
    Guid Subject,
    DateTimeOffset TriggeredAt,
    DateTimeOffset ErasureDue,
    ErasureStatus Status,
    int Attempts,
    IReadOnlyList<SubscriberConfirmationView> Subscribers)
{
    /// <summary>
    /// The view of a takedown's progress.
    /// </summary>
    /// <param name="progress">The progress.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The progress is absent.</exception>
    public static TakedownProgressView Of(TakedownProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new TakedownProgressView(
            progress.Id.Value,
            progress.Subject.Value,
            progress.TriggeredAt,
            progress.ErasureDue,
            progress.Status,
            progress.Attempts,
            [.. progress.Subscribers.Select(SubscriberConfirmationView.Of)]);
    }
}
