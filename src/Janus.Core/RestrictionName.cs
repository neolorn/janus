using System;
using System.Diagnostics.CodeAnalysis;

namespace Janus.Core;

/// <summary>
/// The name of a sending restriction: 1 to 64 lower-case letters and digits separated
/// by single <c>.</c>, <c>-</c> or <c>_</c>.
/// </summary>
/// <remarks>
/// Implements INT-SMS-003, AUTH-ABUSE-004 and CONV-DESIGN-004. A restriction's name
/// fills a message place measured at 64 characters, so a name outside the rule is read
/// nowhere: a route answers it before any body, and a caller in process cannot name a
/// restriction outside it. A default instance was never read, so it has no text to
/// give and no row can carry it.
/// </remarks>
public readonly record struct RestrictionName : IParsable<RestrictionName>
{
    private readonly string? _value;

    private RestrictionName(string value) => _value = value;

    /// <summary>
    /// Reads a restriction name, refusing anything outside the rule.
    /// </summary>
    /// <param name="value">The name.</param>
    /// <returns>The restriction name.</returns>
    /// <exception cref="ArgumentException">The value is not a restriction name.</exception>
    public static RestrictionName Parse(string value)
    {
        if (!TryParse(value, out RestrictionName name))
        {
            throw new ArgumentException(
                "A restriction name is 1 to 64 lower-case letters and digits separated by single full stops, hyphens or underscores.",
                nameof(value));
        }

        return name;
    }

    /// <summary>
    /// Reads a restriction name, answering whether it is one.
    /// </summary>
    /// <param name="value">The name.</param>
    /// <param name="name">The restriction name, where the value is one.</param>
    /// <returns>Whether the value keeps the rule.</returns>
    public static bool TryParse(string? value, out RestrictionName name)
    {
        if (value is null || !PlaceName.Holds(value))
        {
            name = default;
            return false;
        }

        name = new RestrictionName(value);
        return true;
    }

    /// <summary>
    /// Reads a restriction name as a route or a query carries it.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Unused: a restriction name is written one way.</param>
    /// <returns>The value the text names.</returns>
    /// <exception cref="FormatException">The text is not a restriction name.</exception>
    static RestrictionName IParsable<RestrictionName>.Parse(string s, IFormatProvider? provider) =>
        TryParse(s, out RestrictionName result)
            ? result
            : throw new FormatException("The text is not a restriction name.");

    /// <summary>
    /// Reads a restriction name as a route or a query carries it.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Unused: a restriction name is written one way.</param>
    /// <param name="result">The value, where the text names one.</param>
    /// <returns>Whether the text is a restriction name.</returns>
    static bool IParsable<RestrictionName>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out RestrictionName result) =>
        TryParse(s, out result);

    /// <summary>
    /// The name as it crosses the boundary.
    /// </summary>
    /// <returns>The name.</returns>
    /// <exception cref="InvalidOperationException">The restriction name was never set.</exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "CONV-DESIGN-004 AC3: an unset value has no text, and the empty text it would give is what a store writes.")]
    public override string ToString() => _value ?? throw new InvalidOperationException("The restriction name was never set.");
}
