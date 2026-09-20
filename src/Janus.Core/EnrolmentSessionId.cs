using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one enrolment session, which an admin-assisted recovery link
/// opens and which grants no application access.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004, AUTH-RECOV-002 and D-147. It never reaches a browser:
/// the browser carries the pre-authentication cookie, and the session is found from
/// that.
/// </remarks>
public readonly record struct EnrolmentSessionId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a newly opened enrolment session.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static EnrolmentSessionId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new EnrolmentSessionId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
