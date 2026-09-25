using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Janus.Core;

/// <summary>
/// A machine-readable error code: hierarchical, lowercase, dot-separated, and stable.
/// A code is an identifier, never a message.
/// </summary>
/// <remarks>
/// Implements CONV-NAME-003 and CONV-DESIGN-004. The catalogue is
/// <see cref="ErrorCodes"/>. A default instance was never read, so it has no text to
/// give.
/// </remarks>
public readonly partial record struct ErrorCode
{
    private readonly string? _value;

    private ErrorCode(string value) => _value = value;

    /// <summary>
    /// Reads a code, refusing anything outside the format of CONV-NAME-003.
    /// </summary>
    /// <param name="value">The dot-separated code.</param>
    /// <returns>The code.</returns>
    /// <exception cref="ArgumentException">The value is not a well-formed code.</exception>
    /// <remarks>Implements CONV-NAME-003, CONV-DESIGN-004.</remarks>
    public static ErrorCode Parse(string value)
    {
        if (value is null || !Shape().IsMatch(value))
        {
            throw new ArgumentException("An error code is lowercase, dot-separated segments of letters and digits.", nameof(value));
        }

        return new ErrorCode(value);
    }

    /// <summary>
    /// The code as it crosses the boundary.
    /// </summary>
    /// <returns>The dot-separated code.</returns>
    /// <exception cref="InvalidOperationException">The code was never set.</exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "CONV-DESIGN-004 AC3: an unset value has no text, and the empty text it would give is what a store writes.")]
    public override string ToString() => _value ?? throw new InvalidOperationException("The code was never set.");

    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
