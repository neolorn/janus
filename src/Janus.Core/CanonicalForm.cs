using System;
using Janus.Core.Unicode;

namespace Janus.Core;

/// <summary>
/// The single canonical form every identifier is stored and compared under, and the
/// digit mapping a phone number takes instead of one.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004 and IDN-ACCT-006. The form is <c>NFKC_Casefold</c>:
/// compatibility normalization, full case folding and the removal of default-ignorable
/// code points, as one operation. It is computed from tables the package carries at the
/// version <see cref="UnicodeVersion"/> names, never from the machine's own, so the
/// fingerprints of PRIV-RIGHT-005c stay derivable from the version recorded beside
/// them.
/// </remarks>
public static class CanonicalForm
{
    /// <summary>
    /// The Unicode version the canonical form is computed at, recorded beside every
    /// fingerprint as the canonicalisation version.
    /// </summary>
    public static string UnicodeVersion { get; } = Janus.Core.Unicode.UnicodeVersion.Value;

    /// <summary>
    /// The canonical form of a value: the form it is stored and compared under. The
    /// form the person entered is kept separately and is what is shown back to them.
    /// </summary>
    /// <param name="value">The value as it was entered.</param>
    /// <returns>The canonical form.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static string Of(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Canonicalization.Casefold(value);
    }

    /// <summary>
    /// A value with every decimal digit, of whatever script, written as its ASCII
    /// digit. A phone number takes no Unicode form; it takes this and is then stored as
    /// E.164.
    /// </summary>
    /// <param name="value">The value as it was entered.</param>
    /// <returns>The value with ASCII digits.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static string AsciiDigits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Canonicalization.AsciiDigits(value);
    }
}
