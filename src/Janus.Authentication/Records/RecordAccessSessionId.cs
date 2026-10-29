using System;
using System.Globalization;

namespace Janus.Authentication.Records;

/// <summary>
/// The identifier of one record access session, through which a person reaches the
/// person records that hold their email or phone.
/// </summary>
/// <param name="Value">The identifier as the database carries it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and REC-ACC-001. It never reaches a browser: the browser
/// carries the pre-authentication cookie, and the session is found from that.
/// </remarks>
internal readonly record struct RecordAccessSessionId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a newly asked record access session.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static RecordAccessSessionId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new RecordAccessSessionId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
