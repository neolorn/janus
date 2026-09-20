using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one data subject request.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and PRIV-RIGHT-001. Every identifier but the subject's
/// is a version 7 value, so rows written together sit together in the index.
/// </remarks>
public readonly record struct PrivacyRequestId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a request entered at one instant.
    /// </summary>
    /// <param name="at">When it entered the queue.</param>
    /// <returns>An identifier ordered by that instant.</returns>
    public static PrivacyRequestId Of(DateTimeOffset at) =>
        new(Guid.CreateVersion7(at));

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
