using System;
using System.Diagnostics.CodeAnalysis;
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
public readonly record struct SessionId(Guid Value) : IParsable<SessionId>
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

    /// <summary>
    /// Reads an identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The identifier as text.</param>
    /// <param name="provider">Unused: an identifier is written one way.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="FormatException">The text is not an identifier.</exception>
    public static SessionId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    /// <summary>
    /// Reads an identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The identifier as text.</param>
    /// <param name="provider">Unused: an identifier is written one way.</param>
    /// <param name="result">The identifier, where the text is one.</param>
    /// <returns>Whether the text is an identifier.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SessionId result)
    {
        bool parsed = Guid.TryParse(s, provider, out Guid value);

        result = new SessionId(value);

        return parsed;
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
