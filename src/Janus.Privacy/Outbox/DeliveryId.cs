using System;
using System.Globalization;

namespace Janus.Privacy.Outbox;

/// <summary>
/// The identifier of one outbox delivery, which is also the key a subscriber
/// recognises a repeat by.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and IDN-LIFE-003a. A version 7 value, so deliveries
/// written together sit together in the index.
/// </remarks>
internal readonly record struct DeliveryId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a fact raised at one instant.
    /// </summary>
    /// <param name="raisedAt">When the fact became true.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static DeliveryId Of(DateTimeOffset raisedAt) =>
        new(Guid.CreateVersion7(raisedAt));

    /// <summary>
    /// The key a subscriber recognises a repeat by, which is the delivery and never
    /// the attempt.
    /// </summary>
    /// <returns>The key.</returns>
    public string Key() => Value.ToString("N", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
