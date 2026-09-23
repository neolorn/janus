using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one takedown, which is the delivery its trigger wrote for the
/// hosts to confirm against.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004, IDN-LIFE-003 and chapter 09 section 8a. The takedown's
/// progress is the progress of that delivery, so the two share one identifier.
/// </remarks>
public readonly record struct TakedownId(Guid Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
