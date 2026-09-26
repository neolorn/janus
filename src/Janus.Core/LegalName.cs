using System;
using System.Collections.Generic;
using Janus.Core.Unicode;

namespace Janus.Core;

/// <summary>
/// The legal name, a proofing attribute that is off unless the deployment turns it on
/// with a declared purpose.
/// </summary>
/// <remarks>
/// Implements REG-PROF-001, IDN-ATTR-007, IDN-ACCT-005 and CONV-DESIGN-004. Identity
/// does no proofing
/// and needs no name, so nothing here checks a legal name against anything: it is
/// normalized, bounded and required to be of one script per word, and that is all.
/// The bound is in Unicode scalar values, counted after the normalization, so the same
/// name written in two compositions is the same length. A default instance was never
/// read, so it has no name to give and no row can carry it.
/// </remarks>
public readonly record struct LegalName
{
    /// <summary>
    /// The fewest scalar values a legal name carries.
    /// </summary>
    public const int MinimumLength = 1;

    /// <summary>
    /// The most scalar values a legal name carries.
    /// </summary>
    public const int MaximumLength = 200;

    private readonly string? _value;

    private LegalName(string value) => _value = value;

    /// <summary>
    /// The legal name in Normalization Form C, which is what is stored and shown.
    /// </summary>
    /// <exception cref="InvalidOperationException">The legal name was never set.</exception>
    public string Value => _value ?? throw new InvalidOperationException("The legal name was never set.");

    /// <summary>
    /// Reads a legal name as it was entered and returns it in Normalization Form C.
    /// </summary>
    /// <param name="entered">The legal name as it was entered.</param>
    /// <param name="name">The legal name, or an unset value.</param>
    /// <returns>Whether the value is a legal name this library accepts.</returns>
    /// <exception cref="ArgumentNullException">The value is absent.</exception>
    public static bool TryParse(string entered, out LegalName name)
    {
        ArgumentNullException.ThrowIfNull(entered);

        name = default;

        List<int> composed = Normalizer.Nfc(Text.Read(entered));

        if (composed.Count is < MinimumLength or > MaximumLength)
        {
            return false;
        }

        string normalized = Text.Write(composed);

        if (!ScriptMixing.IsSingleScriptPerWord(normalized))
        {
            return false;
        }

        name = new LegalName(normalized);

        return true;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The legal name was never set.</exception>
    public override string ToString() => Value;
}
