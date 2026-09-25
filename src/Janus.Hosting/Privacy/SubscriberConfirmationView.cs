using System;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// How far one subscriber has got with one delivery.
/// </summary>
/// <param name="Name">What the subscriber is called.</param>
/// <param name="Required">Whether the delivery stays open until it confirms.</param>
/// <param name="ConfirmedAt">When it confirmed, or null while it has not.</param>
/// <remarks>Implements chapter 09 section 8a and IDN-LIFE-003a.</remarks>
internal sealed record SubscriberConfirmationView(
    string Name,
    bool Required,
    DateTimeOffset? ConfirmedAt)
{
    /// <summary>
    /// The view of one subscriber's confirmation.
    /// </summary>
    /// <param name="confirmation">The confirmation.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The confirmation is absent.</exception>
    public static SubscriberConfirmationView Of(SubscriberConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);

        return new SubscriberConfirmationView(
            confirmation.Name,
            confirmation.Required,
            confirmation.ConfirmedAt);
    }
}
