using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one enrolled authenticator.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and AUTH-FACT-001. Every identifier but the subject's is
/// a version 7 value, so rows written together sit together in the index.
/// </remarks>
public readonly record struct AuthenticatorId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for one authenticator.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static AuthenticatorId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new AuthenticatorId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
