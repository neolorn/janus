using System;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one session. It names the record and is not what the browser
/// carries: the cookie holds an opaque secret that rotates, this does not.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004, AUTH-SESS-001 and AUTH-SESS-013. Every identifier but
/// the subject's is a version 7 value, so rows written together sit together in the
/// index.
/// </remarks>
[NeverLogged]
public readonly record struct SessionId(Guid Value)
{
    /// <summary>
    /// Issues an identifier for a new session.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static SessionId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new SessionId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
