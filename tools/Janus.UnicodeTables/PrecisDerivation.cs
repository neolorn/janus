using System;
using System.Collections.Generic;

namespace Janus.UnicodeTables;

/// <summary>
/// The PRECIS derived property of every code point, computed by the algorithm of
/// RFC 8264 section 8 from the categories of RFC 8264 section 9 and RFC 5892 section 2.
/// </summary>
/// <remarks>
/// The order of the tests is the RFC's and may not be changed: RFC 8264 section 8 says
/// so, because another order gives another answer for the same Unicode version.
/// </remarks>
internal static class PrecisDerivation
{
    /// <summary>
    /// The values of the property, in the order the table holds them. Disallowed is
    /// first so that a code point no rule reached is refused rather than admitted.
    /// </summary>
    internal static readonly string[] Values =
    [
        "Disallowed",
        "Unassigned",
        "Pvalid",
        "ContextJ",
        "ContextO",
        "FreeformPvalid",
    ];

    // RFC 5892 section 2.6. These are the code points whose value cannot be derived
    // from Unicode properties alone; the RFC lists them and their values.
    private static readonly int[] ExceptionPvalid = [0x00DF, 0x03C2, 0x06FD, 0x06FE, 0x0F0B, 0x3007];

    private static readonly int[] ExceptionContextO =
    [
        0x00B7, 0x0375, 0x05F3, 0x05F4, 0x30FB,
        0x0660, 0x0661, 0x0662, 0x0663, 0x0664, 0x0665, 0x0666, 0x0667, 0x0668, 0x0669,
        0x06F0, 0x06F1, 0x06F2, 0x06F3, 0x06F4, 0x06F5, 0x06F6, 0x06F7, 0x06F8, 0x06F9,
    ];

    private static readonly int[] ExceptionDisallowed =
    [
        0x0640, 0x07FA, 0x302E, 0x302F,
        0x3031, 0x3032, 0x3033, 0x3034, 0x3035, 0x303B,
    ];

    private static readonly string[] LetterDigits = ["Ll", "Lu", "Lo", "Nd", "Lm", "Mn", "Mc"];

    private static readonly string[] OtherLetterDigits = ["Lt", "Nl", "No", "Me"];

    private static readonly string[] Symbols = ["Sm", "Sc", "Sk", "So"];

    private static readonly string[] Punctuation = ["Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po"];

    /// <summary>
    /// The identifier of a value in <see cref="Values"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The identifier the table holds.</returns>
    internal static byte Identifier(string value) => (byte)Array.IndexOf(Values, value);

    /// <summary>
    /// Derives the property of every code point.
    /// </summary>
    /// <param name="database">The properties of every code point.</param>
    /// <param name="normalization">The normalization forms, for the HasCompat category.</param>
    /// <returns>The derived property of every code point.</returns>
    internal static byte[] Derive(UnicodeCharacterDatabase database, Normalization normalization)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(normalization);

        var categories = new Categories(database, normalization);
        byte[] derived = new byte[CodePoints.Count];

        for (int code = 0; code < CodePoints.Count; code++)
        {
            derived[code] = categories.Of(code);
        }

        return derived;
    }

    private sealed class Categories
    {
        private readonly UnicodeCharacterDatabase _database;
        private readonly Dictionary<int, byte> _exceptions = [];
        private readonly HashSet<int> _hasCompat = [];
        private readonly byte _unassignedCategory;
        private readonly byte _control;
        private readonly byte _space;
        private readonly byte[] _letterDigits;
        private readonly byte[] _otherLetterDigits;
        private readonly byte[] _symbols;
        private readonly byte[] _punctuation;
        private readonly byte[] _oldHangulJamo;

        internal Categories(UnicodeCharacterDatabase database, Normalization normalization)
        {
            _database = database;
            _unassignedCategory = database.Aliases.Identifier(PropertyAliases.GeneralCategoryProperty, "Cn");
            _control = database.Aliases.Identifier(PropertyAliases.GeneralCategoryProperty, "Cc");
            _space = database.Aliases.Identifier(PropertyAliases.GeneralCategoryProperty, "Zs");
            _letterDigits = Identifiers(database, LetterDigits);
            _otherLetterDigits = Identifiers(database, OtherLetterDigits);
            _symbols = Identifiers(database, Symbols);
            _punctuation = Identifiers(database, Punctuation);
            _oldHangulJamo =
            [
                database.Aliases.Identifier("hst", "L"),
                database.Aliases.Identifier("hst", "V"),
                database.Aliases.Identifier("hst", "T"),
            ];

            Add(ExceptionPvalid, Identifier("Pvalid"));
            Add(ExceptionContextO, Identifier("ContextO"));
            Add(ExceptionDisallowed, Identifier("Disallowed"));

            foreach (int code in normalization.Compatibility.Keys)
            {
                MarkCompat(normalization, code);
            }

            foreach (int code in normalization.Canonical.Keys)
            {
                MarkCompat(normalization, code);
            }
        }

        internal byte Of(int code)
        {
            // RFC 8264 section 8. BackwardCompatible, the second test, is the empty set
            // in RFC 5892 section 2.7 and has stayed empty, so no test stands for it.
            if (_exceptions.TryGetValue(code, out byte exception))
            {
                return exception;
            }

            if (_database.GeneralCategories[code] == _unassignedCategory && !_database.Noncharacters[code])
            {
                return Identifier("Unassigned");
            }

            if (code is >= 0x21 and <= 0x7E)
            {
                return Identifier("Pvalid");
            }

            if (_database.JoinControls[code])
            {
                return Identifier("ContextJ");
            }

            if (Array.IndexOf(_oldHangulJamo, _database.HangulSyllableTypes[code]) >= 0
                || _database.DefaultIgnorables[code]
                || _database.Noncharacters[code]
                || _database.GeneralCategories[code] == _control)
            {
                return Identifier("Disallowed");
            }

            return Remaining(code);
        }

        private byte Remaining(int code)
        {
            if (_hasCompat.Contains(code))
            {
                return Identifier("FreeformPvalid");
            }

            byte category = _database.GeneralCategories[code];

            if (Array.IndexOf(_letterDigits, category) >= 0)
            {
                return Identifier("Pvalid");
            }

            if (Array.IndexOf(_otherLetterDigits, category) >= 0
                || category == _space
                || Array.IndexOf(_symbols, category) >= 0
                || Array.IndexOf(_punctuation, category) >= 0)
            {
                return Identifier("FreeformPvalid");
            }

            return Identifier("Disallowed");
        }

        private static byte[] Identifiers(UnicodeCharacterDatabase database, string[] categories)
        {
            byte[] identifiers = new byte[categories.Length];

            for (int index = 0; index < categories.Length; index++)
            {
                identifiers[index] =
                    database.Aliases.Identifier(PropertyAliases.GeneralCategoryProperty, categories[index]);
            }

            return identifiers;
        }

        private void MarkCompat(Normalization normalization, int code)
        {
            // HasCompat, RFC 8264 section 9.17: the code point is not itself under
            // Normalization Form KC. A code point the database gives no decomposition
            // for is its own form, so only the decomposable ones are asked.
            int[] normalized = normalization.Nfkc([code]);

            if (normalized.Length != 1 || normalized[0] != code)
            {
                _hasCompat.Add(code);
            }
        }

        private void Add(int[] codes, byte value)
        {
            foreach (int code in codes)
            {
                _exceptions[code] = value;
            }
        }
    }
}
