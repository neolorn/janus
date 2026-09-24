using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one erasure, which is the delivery its host-side work travels on.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004, IDN-LIFE-003a, IDN-LIFE-003b and chapter 09 section 8a.
/// The erasure's progress is the progress of that delivery, so the two share one
/// identifier.
/// </remarks>
public readonly record struct ErasureId(Guid Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
