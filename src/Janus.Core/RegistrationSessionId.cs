using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one registration session, which stages what registration collects
/// and reserves nothing.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and REG-SESS-001. It never reaches a browser: the browser
/// carries the pre-authentication cookie, and the session is found from that.
/// </remarks>
public readonly record struct RegistrationSessionId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a newly created registration session.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static RegistrationSessionId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new RegistrationSessionId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
