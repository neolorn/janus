using System;
using System.Globalization;

namespace Janus.Authentication.Alerting;

/// <summary>
/// The identifier of one raised condition waiting for the alert channels.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and OPS-ALERT-001. A version 7 value, so the conditions
/// are carried in the order they were raised.
/// </remarks>
internal readonly record struct RaisedAlertId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a condition raised at one instant.
    /// </summary>
    /// <param name="raisedAt">When it was raised.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static RaisedAlertId Of(DateTimeOffset raisedAt) =>
        new(Guid.CreateVersion7(raisedAt));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
