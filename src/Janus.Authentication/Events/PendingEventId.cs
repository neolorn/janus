using System;
using System.Globalization;

namespace Janus.Authentication.Events;

/// <summary>
/// The identifier of one emitted event waiting for its consumers.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and LIB-API-001. A version 7 value, so the events are
/// offered by the instant they were raised. Events of one millisecond carry no order
/// among themselves, and one a consumer refused waits out its delay while later ones
/// reach it; delivery promises each consumer the event, not its place.
/// </remarks>
internal readonly record struct PendingEventId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for an event raised at one instant.
    /// </summary>
    /// <param name="raisedAt">When it was raised.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static PendingEventId Of(DateTimeOffset raisedAt) =>
        new(Guid.CreateVersion7(raisedAt));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
