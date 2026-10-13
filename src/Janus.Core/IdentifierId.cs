using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Janus.Core;

/// <summary>
/// The identifier of one of an account's identifiers, as the identifier endpoints name
/// it.
/// </summary>
/// <param name="Value">The identifier as the database and the wire carry it.</param>
/// <remarks>
/// Implements CONV-DESIGN-004 and REG-IDENT-002. Every identifier but the subject's is
/// a version 7 value, so rows written together sit together in the index.
/// </remarks>
public readonly record struct IdentifierId(Guid Value) : IParsable<IdentifierId>
{
    /// <summary>
    /// Issues an identifier for one newly added to an account.
    /// </summary>
    /// <param name="time">The clock the deployment runs on.</param>
    /// <returns>An identifier ordered by the instant it was issued.</returns>
    /// <exception cref="ArgumentNullException">The clock is absent.</exception>
    public static IdentifierId New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        return new IdentifierId(Guid.CreateVersion7(time.GetUtcNow()));
    }

    /// <summary>
    /// Reads an identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The identifier as text.</param>
    /// <param name="provider">Unused: an identifier is written one way.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="FormatException">The text is not an identifier.</exception>
    public static IdentifierId Parse(string s, IFormatProvider? provider) => new(Guid.Parse(s, provider));

    /// <summary>
    /// Reads an identifier as a route or a query carries it.
    /// </summary>
    /// <param name="s">The identifier as text.</param>
    /// <param name="provider">Unused: an identifier is written one way.</param>
    /// <param name="result">The identifier, where the text is one.</param>
    /// <returns>Whether the text is an identifier.</returns>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out IdentifierId result)
    {
        bool parsed = Guid.TryParse(s, provider, out Guid value);

        result = new IdentifierId(value);

        return parsed;
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString("D", CultureInfo.InvariantCulture);
}
