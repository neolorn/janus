using System;
using System.Diagnostics.CodeAnalysis;

namespace Janus.Core;

/// <summary>
/// The name of a legal document a deployment publishes: 1 to 64 lower-case letters
/// and digits separated by single <c>.</c>, <c>-</c> or <c>_</c>.
/// </summary>
/// <remarks>
/// Implements INT-SMS-003 and CONV-DESIGN-004. A document's name fills a message
/// place measured at 64 characters, so a name outside the rule is read nowhere: a
/// route answers it before any body, and a caller in process cannot publish under it.
/// A declaration names a document as text, judged at startup. A default instance was
/// never read, so it has no text to give and no row can carry it.
/// </remarks>
public readonly record struct DocumentName : IParsable<DocumentName>
{
    private readonly string? _value;

    private DocumentName(string value) => _value = value;

    /// <summary>
    /// Reads a document name, refusing anything outside the rule.
    /// </summary>
    /// <param name="value">The name.</param>
    /// <returns>The document name.</returns>
    /// <exception cref="ArgumentException">The value is not a document name.</exception>
    public static DocumentName Parse(string value)
    {
        if (!TryParse(value, out DocumentName name))
        {
            throw new ArgumentException(
                "A document name is 1 to 64 lower-case letters and digits separated by single full stops, hyphens or underscores.",
                nameof(value));
        }

        return name;
    }

    /// <summary>
    /// Reads a document name, answering whether it is one.
    /// </summary>
    /// <param name="value">The name.</param>
    /// <param name="name">The document name, where the value is one.</param>
    /// <returns>Whether the value keeps the rule.</returns>
    public static bool TryParse(string? value, out DocumentName name)
    {
        if (value is null || !PlaceName.Holds(value))
        {
            name = default;
            return false;
        }

        name = new DocumentName(value);
        return true;
    }

    /// <summary>
    /// Reads a document name as a route or a query carries it.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Unused: a document name is written one way.</param>
    /// <returns>The value the text names.</returns>
    /// <exception cref="FormatException">The text is not a document name.</exception>
    static DocumentName IParsable<DocumentName>.Parse(string s, IFormatProvider? provider) =>
        TryParse(s, out DocumentName result)
            ? result
            : throw new FormatException("The text is not a document name.");

    /// <summary>
    /// Reads a document name as a route or a query carries it.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="provider">Unused: a document name is written one way.</param>
    /// <param name="result">The value, where the text names one.</param>
    /// <returns>Whether the text is a document name.</returns>
    static bool IParsable<DocumentName>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out DocumentName result) =>
        TryParse(s, out result);

    /// <summary>
    /// The name as it crosses the boundary.
    /// </summary>
    /// <returns>The name.</returns>
    /// <exception cref="InvalidOperationException">The document name was never set.</exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "CONV-DESIGN-004 AC3: an unset value has no text, and the empty text it would give is what a store writes.")]
    public override string ToString() => _value ?? throw new InvalidOperationException("The document name was never set.");
}
