using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Janus.UnicodeTables;

/// <summary>
/// Writes the generated tables into <c>Janus.Core</c>.
/// </summary>
/// <remarks>
/// A property every code point has is written as a run-length table: a sorted array of
/// the code point each run starts at, beside the value the run carries. A mapping few
/// code points have is written as a sorted array of those code points, an array of
/// offsets into the data, and the data.
/// </remarks>
internal static class TableEmitter
{
    /// <summary>
    /// Derives every table and writes it.
    /// </summary>
    /// <param name="database">The properties of every code point.</param>
    /// <param name="directory">The directory to write into.</param>
    /// <param name="version">The Unicode version.</param>
    internal static void Emit(UnicodeCharacterDatabase database, string directory, string version)
    {
        ArgumentNullException.ThrowIfNull(database);

        var normalization = new Normalization(database);

        VerifyCasefold(database, normalization);

        byte[] precis = PrecisDerivation.Derive(database, normalization);
        var sets = ScriptSets.Build(database);

        EmitVersion(directory, version);
        EmitEnumerations(database, directory, version);
        EmitNormalization(database, normalization, directory, version);
        EmitCase(database, directory, version);
        EmitCanonicalForm(database, directory, version);
        EmitCategories(database, directory, version);
        EmitPrecis(database, precis, directory, version);
        EmitScripts(database, sets, directory, version);
    }

    private static void EmitVersion(string directory, string version)
    {
        var file = new SourceFile(version);

        file.OpenClass(
            "UnicodeVersion",
            "The Unicode version every table is generated from, and the canonicalisation"
                + " version recorded beside every fingerprint (IDN-ACCT-004).");
        file.Constant("Value", "The version.", version);
        file.CloseClass();
        file.Save(Path.Combine(directory, "UnicodeVersion.cs"));
    }

    private static void EmitEnumerations(UnicodeCharacterDatabase database, string directory, string version)
    {
        Enumeration(
            directory,
            version,
            "GeneralCategory",
            "The General_Category of a code point.",
            database.Aliases.Values(PropertyAliases.GeneralCategoryProperty));

        Enumeration(
            directory,
            version,
            "PrecisProperty",
            "The PRECIS derived property of a code point (RFC 8264 section 8).",
            PrecisDerivation.Values);

        Enumeration(
            directory,
            version,
            "BidiClass",
            "The Bidi_Class of a code point, which the Bidi Rule of RFC 5893 tests.",
            database.Aliases.Values("bc"));

        Enumeration(
            directory,
            version,
            "JoiningType",
            "The Joining_Type of a code point, which the contextual rule for the zero"
                + " width non-joiner tests (RFC 5892 appendix A.1).",
            database.Aliases.Values("jt"));

        Enumeration(
            directory,
            version,
            "ScriptCode",
            "The Script of a code point, followed by the multi-script writing systems"
                + " UTS #39 section 5.1 augments a script set with.",
            database.Aliases.Values(PropertyAliases.ScriptProperty));
    }

    private static void Enumeration(
        string directory,
        string version,
        string name,
        string summary,
        IReadOnlyList<string> members)
    {
        var file = new SourceFile(version);

        file.Enumeration(name, summary, members);
        file.Save(Path.Combine(directory, name + ".cs"));
    }

    private static void EmitNormalization(
        UnicodeCharacterDatabase database,
        Normalization normalization,
        string directory,
        string version)
    {
        var file = new SourceFile(version);

        file.OpenClass(
            "NormalizationTables",
            "The decompositions, combining classes and primary composites of UAX #15,"
                + " so that the library's own normalization is the pinned version's.");

        Runs(file, "CombiningClass", "the Canonical_Combining_Class", database.CombiningClasses, first: true);
        Mapping(file, "Canonical", "the full canonical decomposition", normalization.Canonical, first: false);
        Mapping(file, "Compatibility", "the full compatibility decomposition", normalization.Compatibility, first: false);
        Mapping(file, "Width", "the width mapping of RFC 8265 section 3.3.1", WidthMappings(database), first: false);

        long[] pairs = [.. normalization.Composition.Keys.Order()];

        file.Array(
            "long",
            "CompositionPairs",
            "The starter and following code point of each primary composite, packed.",
            [.. pairs.Select(SourceFile.HexPair)],
            first: false);

        file.Array(
            "int",
            "CompositionValues",
            "The primary composite each pair forms.",
            [.. pairs.Select(pair => SourceFile.Hex(normalization.Composition[pair]))],
            first: false);

        file.CloseClass();
        file.Save(Path.Combine(directory, "NormalizationTables.cs"));
    }

    private static Dictionary<int, int[]> WidthMappings(UnicodeCharacterDatabase database)
    {
        var mappings = new Dictionary<int, int[]>();

        foreach ((int code, string tag) in database.CompatibilityTags)
        {
            if (tag is "wide" or "narrow")
            {
                mappings[code] = database.CompatibilityDecompositions[code];
            }
        }

        return mappings;
    }

    private static void EmitCase(UnicodeCharacterDatabase database, string directory, string version)
    {
        var file = new SourceFile(version);

        file.OpenClass(
            "CaseTables",
            "The full lowercase mapping of Unicode section 3.13, and the two properties"
                + " its final-sigma condition tests.");

        Mapping(file, "Lowercase", "the full lowercase mapping", database.LowercaseMappings, first: true);
        Runs(file, "Cased", "the Cased property", Flags(database.Cased), first: false);
        Runs(file, "CaseIgnorable", "the Case_Ignorable property", Flags(database.CaseIgnorable), first: false);

        file.CloseClass();
        file.Save(Path.Combine(directory, "CaseTables.cs"));
    }

    private static void EmitCanonicalForm(UnicodeCharacterDatabase database, string directory, string version)
    {
        var file = new SourceFile(version);

        file.OpenClass(
            "CanonicalFormTables",
            "The NFKC_Casefold mapping, which is the canonical form of every identifier,"
                + " and the decimal digit values the E.164 mapping reads.");

        Mapping(file, "Casefold", "the NFKC_CF mapping", database.NfkcCasefold, first: true);

        int[] digits = [.. Enumerable.Range(0, CodePoints.Count).Where(code => database.DecimalDigits[code] >= 0)];

        file.Array(
            "int",
            "DigitKeys",
            "The code points that are decimal digits, in order.",
            [.. digits.Select(SourceFile.Hex)],
            first: false);

        file.Array(
            "byte",
            "DigitValues",
            "The value of each of those digits.",
            [.. digits.Select(code => SourceFile.Decimal(database.DecimalDigits[code]))],
            first: false);

        file.CloseClass();
        file.Save(Path.Combine(directory, "CanonicalFormTables.cs"));
    }

    private static void EmitCategories(UnicodeCharacterDatabase database, string directory, string version)
    {
        var file = new SourceFile(version);

        file.OpenClass("CategoryTables", "The General_Category of every code point.");
        Runs(file, "Category", "the General_Category", database.GeneralCategories, first: true);
        file.CloseClass();
        file.Save(Path.Combine(directory, "CategoryTables.cs"));
    }

    private static void EmitPrecis(
        UnicodeCharacterDatabase database,
        byte[] precis,
        string directory,
        string version)
    {
        var file = new SourceFile(version);

        file.OpenClass(
            "PrecisTables",
            "The PRECIS derived property of every code point, and the two properties the"
                + " Bidi Rule and the contextual rules read.");

        Runs(file, "Property", "the PRECIS derived property", precis, first: true);
        Runs(file, "BidiClass", "the Bidi_Class", database.BidiClasses, first: false);
        Runs(file, "JoiningType", "the Joining_Type", database.JoiningTypes, first: false);

        file.CloseClass();
        file.Save(Path.Combine(directory, "PrecisTables.cs"));
    }

    private static void EmitScripts(
        UnicodeCharacterDatabase database,
        ScriptSets sets,
        string directory,
        string version)
    {
        var file = new SourceFile(version);

        file.OpenClass(
            "ScriptTables",
            "The Script of every code point, and the augmented script set of UTS #39"
                + " section 5.1 that mixed-script detection intersects.");

        Runs(file, "Script", "the Script", database.Scripts, first: true);
        Runs(file, "Set", "the augmented script set", sets.ByCodePoint, first: false);

        var offsets = new List<int>(sets.Sets.Count + 1) { 0 };
        var members = new List<short>();

        foreach (short[] set in sets.Sets)
        {
            members.AddRange(set);
            offsets.Add(members.Count);
        }

        file.Array(
            "int",
            "SetOffsets",
            "Where each set's members begin and end. The first set is empty and stands"
                + " for every script.",
            [.. offsets.Select(SourceFile.Decimal)],
            first: false);

        file.Array(
            "short",
            "SetMembers",
            "The scripts of every set, each set's own in ascending order.",
            [.. members.Select(member => SourceFile.Decimal(member))],
            first: false);

        file.CloseClass();
        file.Save(Path.Combine(directory, "ScriptTables.cs"));
    }

    private static byte[] Flags(bool[] property)
    {
        byte[] flags = new byte[property.Length];

        for (int code = 0; code < property.Length; code++)
        {
            flags[code] = property[code] ? (byte)1 : (byte)0;
        }

        return flags;
    }

    private static void Runs(SourceFile file, string name, string what, byte[] values, bool first)
    {
        (List<int> starts, List<byte> carried) = Compress(values);

        file.Array(
            "int",
            name + "Starts",
            "The code point each run of " + what + " starts at.",
            [.. starts.Select(SourceFile.Hex)],
            first);

        file.Array(
            "byte",
            name + "Values",
            "The value each of those runs carries.",
            [.. carried.Select(value => SourceFile.Decimal(value))],
            first: false);
    }

    private static void Runs(SourceFile file, string name, string what, short[] values, bool first)
    {
        (List<int> starts, List<short> carried) = Compress(values);

        file.Array(
            "int",
            name + "Starts",
            "The code point each run of " + what + " starts at.",
            [.. starts.Select(SourceFile.Hex)],
            first);

        file.Array(
            "short",
            name + "Values",
            "The value each of those runs carries.",
            [.. carried.Select(value => SourceFile.Decimal(value))],
            first: false);
    }

    private static (List<int> Starts, List<TValue> Values) Compress<TValue>(TValue[] values)
        where TValue : struct, IEquatable<TValue>
    {
        var starts = new List<int>();
        var carried = new List<TValue>();

        for (int code = 0; code < values.Length; code++)
        {
            if (code == 0 || !values[code].Equals(values[code - 1]))
            {
                starts.Add(code);
                carried.Add(values[code]);
            }
        }

        return (starts, carried);
    }

    private static void Mapping(
        SourceFile file,
        string name,
        string what,
        IReadOnlyDictionary<int, int[]> mappings,
        bool first)
    {
        int[] keys = [.. mappings.Keys.Order()];
        var offsets = new List<int>(keys.Length + 1) { 0 };
        var data = new List<int>();

        foreach (int key in keys)
        {
            data.AddRange(mappings[key]);
            offsets.Add(data.Count);
        }

        file.Array(
            "int",
            name + "Keys",
            "The code points " + what + " is given for, in order.",
            [.. keys.Select(SourceFile.Hex)],
            first);

        file.Array(
            "int",
            name + "Offsets",
            "Where each of those mappings begins and ends.",
            [.. offsets.Select(SourceFile.Decimal)],
            first: false);

        file.Array(
            "int",
            name + "Data",
            "The code points each of those mappings produces.",
            [.. data.Select(SourceFile.Hex)],
            first: false);
    }

    private static void VerifyCasefold(UnicodeCharacterDatabase database, Normalization normalization)
    {
        // NFKC_Casefold is constructed by applying NFKC, full case folding and the
        // removal of default-ignorable code points until the result is stable (UAX #44).
        // Deriving it here from those three and comparing it to the database is what
        // proves the shipped mapping is the composition the specification names, rather
        // than a column copied on trust.
        foreach (int code in Changeable(database, normalization))
        {
            int[] derived = DeriveCasefold(database, normalization, code);
            int[] expected = database.NfkcCasefold.TryGetValue(code, out int[]? mapping) ? mapping : [code];

            if (!derived.SequenceEqual(expected))
            {
                throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"NFKC_CF of U+{code:X4} derives to a different mapping than the database gives."));
            }
        }
    }

    private static SortedSet<int> Changeable(UnicodeCharacterDatabase database, Normalization normalization)
    {
        var changeable = new SortedSet<int>(database.NfkcCasefold.Keys);

        changeable.UnionWith(database.CaseFoldings.Keys);
        changeable.UnionWith(normalization.Compatibility.Keys);
        changeable.UnionWith(normalization.Canonical.Keys);

        for (int code = 0; code < CodePoints.Count; code++)
        {
            if (database.DefaultIgnorables[code])
            {
                changeable.Add(code);
            }
        }

        return changeable;
    }

    private static int[] DeriveCasefold(
        UnicodeCharacterDatabase database,
        Normalization normalization,
        int code)
    {
        int[] current = [code];

        for (int pass = 0; pass < 4; pass++)
        {
            var folded = new List<int>(current.Length + 2);

            foreach (int part in normalization.Nfkc(current))
            {
                if (database.CaseFoldings.TryGetValue(part, out int[]? folding))
                {
                    folded.AddRange(folding);
                }
                else
                {
                    folded.Add(part);
                }
            }

            folded.RemoveAll(part => database.DefaultIgnorables[part]);

            int[] next = normalization.Nfkc(folded);

            if (next.SequenceEqual(current))
            {
                return current;
            }

            current = next;
        }

        return current;
    }
}
