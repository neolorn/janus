using System;
using System.Globalization;

namespace Janus.Identity.Identifiers;

/// <summary>
/// The identifier of one removal row: one value an account gave up, by a removal or a
/// replace.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and REG-IDENT-006. A removal is keyed by an identifier of
/// its own and names the identifier it came from, so one identifier changed twice
/// within the window stands behind two removals. A version 7 value, so the removals
/// written together sit together in the index.
/// </remarks>
internal readonly record struct IdentifierRemovalId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a removal made at one instant.
    /// </summary>
    /// <param name="removedAt">When the value was given up.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static IdentifierRemovalId Of(DateTimeOffset removedAt) =>
        new(Guid.CreateVersion7(removedAt));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
