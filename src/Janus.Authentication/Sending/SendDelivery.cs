using System;
using Janus.Core;

namespace Janus.Authentication.Sending;

/// <summary>
/// One message the library has undertaken to send, written in the transaction that
/// made it necessary and carried afterwards.
/// </summary>
/// <param name="Id">What the row is held under.</param>
/// <param name="RecordedAt">When the send was recorded.</param>
/// <param name="Requested">What is to be sent.</param>
/// <remarks>
/// Implements D-022 and IDN-PRIN-003. A message is a working artefact: the row exists
/// so that a message undertaken inside a transaction is not lost with the process that
/// took it, and it is removed once a transport has taken it.
/// </remarks>
internal sealed record SendDelivery(
    SendDeliveryId Id,
    DateTimeOffset RecordedAt,
    SendRequest Requested)
{
    /// <summary>
    /// A message just undertaken.
    /// </summary>
    /// <param name="request">What is to be sent.</param>
    /// <param name="recordedAt">When it was undertaken.</param>
    /// <returns>The delivery.</returns>
    /// <exception cref="ArgumentNullException">The request is absent.</exception>
    public static SendDelivery Of(SendRequest request, DateTimeOffset recordedAt)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SendDelivery(SendDeliveryId.Of(recordedAt), recordedAt, request);
    }
}
