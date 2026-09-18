using System;
using System.Text.RegularExpressions;

namespace Janus.Core;

/// <summary>
/// A machine-readable error code: hierarchical, lowercase, dot-separated, and stable.
/// A code is an identifier, never a message.
/// </summary>
/// <remarks>Implements CONV-NAME-003. The catalogue is <see cref="ErrorCodes"/>.</remarks>
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
    /// <returns>The dot-separated code, or an empty string for an unset code.</returns>
    public override string ToString() => _value ?? string.Empty;

    [GeneratedRegex("^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
