using System;
using System.Collections.Generic;
using System.Linq;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// Where a delivery of host-side work has got, of whichever kind.
/// </summary>
/// <param name="Id">What the delivery is held under, whatever its kind.</param>
/// <param name="Kind">The kind of host-side work it carries.</param>
/// <param name="Subject">Whose it is.</param>
/// <param name="Purpose">The purpose a consent or an objection delivery concerns, or null.</param>
/// <param name="Change">The change its event announces, or null for an erasure or a takedown.</param>
/// <param name="Reason">Why an erasure ran, or null for another kind.</param>
/// <param name="Status">How far the subscribers or handlers have got.</param>
/// <param name="Attempts">How many delivery attempts have been made across them.</param>
/// <param name="Subscribers">Each subscriber or purpose handler it is owed to and whether it confirmed.</param>
/// <remarks>Implements chapter 09 section 8a, IDN-LIFE-003a, IDN-LIFE-003b and D-203.</remarks>
internal sealed record ErasureProgressView(
    Guid Id,
    DeliveryKind Kind,
    Guid Subject,
    string? Purpose,
    DeliveryChange? Change,
    ErasureReason? Reason,
    ErasureStatus Status,
    int Attempts,
    IReadOnlyList<SubscriberConfirmationView> Subscribers)
{
    /// <summary>
    /// The view of a delivery's progress.
    /// </summary>
    /// <param name="progress">The progress.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The progress is absent.</exception>
    public static ErasureProgressView Of(ErasureProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new ErasureProgressView(
            progress.Id.Value,
            progress.Kind,
            progress.Subject.Value,
            progress.Purpose,
            progress.Change,
            progress.Reason,
            progress.Status,
            progress.Attempts,
            [.. progress.Subscribers.Select(SubscriberConfirmationView.Of)]);
    }
}
