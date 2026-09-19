using System;
using System.Text.RegularExpressions;

namespace Janus.Core;

/// <summary>
/// What an audit record says happened: a machine-readable code, hierarchical,
/// lowercase and dot-separated, rendered into words at display and never stored as
/// words.
/// </summary>
/// <remarks>
/// Implements IDN-AUD-001, PRIV-RET-004 and CONV-NAME-003. An audit trail whose text
/// varies by locale is not queryable and does not mean the same thing to two readers,
/// so the row carries the code and the frontend carries the sentence.
/// </remarks>
public readonly partial record struct AuditAction
{
    private readonly string? _value;

    private AuditAction(string value) => _value = value;

    /// <summary>
    /// Reads an action, refusing anything outside the shape of a code.
    /// </summary>
    /// <param name="value">The dot-separated code.</param>
    /// <returns>The action.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed code.</exception>
    public static AuditAction Parse(string value)
    {
        if (value is null || !Shape().IsMatch(value))
        {
            throw new ArgumentException(
                "An audit action is lowercase, dot-separated segments of letters and digits.",
                nameof(value));
        }

        return new AuditAction(value);
    }

    /// <summary>
    /// The action as the row and the wire carry it.
    /// </summary>
    /// <returns>The dot-separated code, or an empty string for an unset action.</returns>
    public override string ToString() => _value ?? string.Empty;

    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
