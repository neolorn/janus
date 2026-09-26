using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// What one licence or permit is known by. The management application names it, so
/// the list replaced twice with the same request stands as it did after the first.
/// </summary>
/// <param name="Value">The identifier.</param>
/// <remarks>Implements OPS-MAINT-001.</remarks>
public readonly record struct LicenceId(Guid Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
