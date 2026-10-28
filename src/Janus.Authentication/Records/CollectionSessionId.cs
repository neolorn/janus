using System;
using System.Globalization;

namespace Janus.Authentication.Records;

/// <summary>
/// The identifier of one collection session, which stages the age answer and the
/// contacts a host's screen collects and reserves nothing.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and REC-COL-001. It never reaches a browser: the browser
/// carries the pre-authentication cookie, and the session is found from that.
/// </remarks>
internal readonly record struct CollectionSessionId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a newly created collection session.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static CollectionSessionId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new CollectionSessionId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
