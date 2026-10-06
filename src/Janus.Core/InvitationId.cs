using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one invitation into an organization.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>Implements CONV-DESIGN-004, IDN-LIFE-009a and chapter 09 section 8a.</remarks>
public readonly record struct InvitationId(Guid Value) : IParsable<InvitationId>
{
    /// <summary>
    /// Issues an identifier for one invitation.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static InvitationId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new InvitationId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <summary>
    /// Reads an identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The identifier as text.</param>
    /// <param name="provider">Unused: an identifier is written one way.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="FormatException">The text is not an identifier.</exception>
    public static InvitationId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    /// <summary>
    /// Reads an identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The identifier as text.</param>
    /// <param name="provider">Unused: an identifier is written one way.</param>
    /// <param name="result">The identifier, where the text is one.</param>
    /// <returns>Whether the text is an identifier.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out InvitationId result)
    {
        bool parsed = Guid.TryParse(s, provider, out Guid value);

        result = new InvitationId(value);

        return parsed;
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
