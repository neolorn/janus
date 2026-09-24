using System;
using System.Globalization;

namespace Janus.Authentication.BreakGlass;

/// <summary>
/// The identifier of one issue of the break-glass credential.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and OPS-BOOT-004. A version 7 value, so the issues sit in
/// the order they were generated.
/// </remarks>
internal readonly record struct BreakGlassCredentialId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a credential generated at one instant.
    /// </summary>
    /// <param name="issuedAt">When it was generated.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static BreakGlassCredentialId Of(DateTimeOffset issuedAt) =>
        new(Guid.CreateVersion7(issuedAt));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
