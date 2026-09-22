using System;
using System.Globalization;

namespace Janus.Authentication.Sending;

/// <summary>
/// The identifier of one message waiting to be carried.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and D-022. A version 7 value, so the messages written
/// together sit together in the index the publisher reads them in.
/// </remarks>
internal readonly record struct SendDeliveryId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a message recorded at one instant.
    /// </summary>
    /// <param name="recordedAt">When the send was recorded.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static SendDeliveryId Of(DateTimeOffset recordedAt) =>
        new(Guid.CreateVersion7(recordedAt));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
