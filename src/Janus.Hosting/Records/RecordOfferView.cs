using System;
using Janus.Core;

namespace Janus.Hosting.Records;

/// <summary>
/// The offer to attach, as <c>GET /account/records/offer</c> answers it: a count and its
/// expiry, nothing of the records.
/// </summary>
/// <param name="Count">How many records the offer counts.</param>
/// <param name="ExpiresAt">When it ends, or nothing where no offer stands.</param>
/// <remarks>Implements REC-ATT-001, chapter 09 section 7a.</remarks>
internal sealed record RecordOfferView(int Count, DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Shows an offer.
    /// </summary>
    /// <param name="offer">The offer.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The offer is absent.</exception>
    public static RecordOfferView Of(RecordOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        return new RecordOfferView(offer.Count, offer.ExpiresAt);
    }
}
