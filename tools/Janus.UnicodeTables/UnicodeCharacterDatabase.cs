using System;
using System.Collections.Generic;
using System.Globalization;

namespace Janus.UnicodeTables;

/// <summary>
/// Every property of every code point the tables are derived from, read once from the
/// vendored files.
/// </summary>
/// <remarks>
/// A property that holds for most code points is kept in one array indexed by code
/// point; a mapping, which holds for few, is kept in a dictionary. Values the database
/// spells in two ways, a long name in one file and a short one in another, are held
/// here as the identifier <see cref="PropertyAliases"/> gives them.
/// </remarks>
internal sealed class UnicodeCharacterDatabase
{
    private UnicodeCharacterDatabase(PropertyAliases aliases)
    {
        Aliases = aliases;
        GeneralCategories = new byte[CodePoints.Count];
        CombiningClasses = new byte[CodePoints.Count];
        BidiClasses = new byte[CodePoints.Count];
        JoiningTypes = new byte[CodePoints.Count];
        Scripts = new short[CodePoints.Count];
        HangulSyllableTypes = new byte[CodePoints.Count];
        DecimalDigits = new sbyte[CodePoints.Count];
        FullCompositionExclusions = new bool[CodePoints.Count];
        DefaultIgnorables = new bool[CodePoints.Count];
        Cased = new bool[CodePoints.Count];
        CaseIgnorable = new bool[CodePoints.Count];
        JoinControls = new bool[CodePoints.Count];
        Noncharacters = new bool[CodePoints.Count];
        CanonicalDecompositions = [];
        CompatibilityDecompositions = [];
        CompatibilityTags = [];
        LowercaseMappings = [];
        CaseFoldings = [];
        NfkcCasefold = [];
        ScriptExtensions = [];
    }

    /// <summary>
    /// The property value names behind the identifiers the arrays hold.
    /// </summary>
    internal PropertyAliases Aliases { get; }

    /// <summary>
    /// The General_Category of each code point.
    /// </summary>
    internal byte[] GeneralCategories { get; }

    /// <summary>
    /// The Canonical_Combining_Class of each code point.
    /// </summary>
    internal byte[] CombiningClasses { get; }

    /// <summary>
    /// The Bidi_Class of each code point.
    /// </summary>
    internal byte[] BidiClasses { get; }

    /// <summary>
    /// The Joining_Type of each code point.
    /// </summary>
    internal byte[] JoiningTypes { get; }

    /// <summary>
    /// The Script of each code point.
    /// </summary>
    internal short[] Scripts { get; }

    /// <summary>
    /// The Hangul_Syllable_Type of each code point.
    /// </summary>
    internal byte[] HangulSyllableTypes { get; }

    /// <summary>
    /// The decimal digit value of each code point, or minus one where it has none.
    /// </summary>
    internal sbyte[] DecimalDigits { get; }

    /// <summary>
    /// Whether each code point is excluded from canonical composition.
    /// </summary>
    internal bool[] FullCompositionExclusions { get; }

    /// <summary>
    /// Whether each code point has Default_Ignorable_Code_Point.
    /// </summary>
    internal bool[] DefaultIgnorables { get; }

    /// <summary>
    /// Whether each code point is Cased.
    /// </summary>
    internal bool[] Cased { get; }

    /// <summary>
    /// Whether each code point is Case_Ignorable.
    /// </summary>
    internal bool[] CaseIgnorable { get; }

    /// <summary>
    /// Whether each code point is a Join_Control.
    /// </summary>
    internal bool[] JoinControls { get; }

    /// <summary>
    /// Whether each code point is a Noncharacter_Code_Point.
    /// </summary>
    internal bool[] Noncharacters { get; }

    /// <summary>
    /// The one-step canonical decomposition of the code points that have one.
    /// </summary>
    internal Dictionary<int, int[]> CanonicalDecompositions { get; private set; }

    /// <summary>
    /// The one-step compatibility decomposition of the code points that have one.
    /// </summary>
    internal Dictionary<int, int[]> CompatibilityDecompositions { get; private set; }

    /// <summary>
    /// The formatting tag of each compatibility decomposition, such as the width tags
    /// the PRECIS width mapping rule is drawn from.
    /// </summary>
    internal Dictionary<int, string> CompatibilityTags { get; private set; }

    /// <summary>
    /// The full lowercase mapping of the code points whose mapping is not themselves.
    /// </summary>
    internal Dictionary<int, int[]> LowercaseMappings { get; private set; }

    /// <summary>
    /// The full case folding of the code points whose folding is not themselves.
    /// </summary>
    internal Dictionary<int, int[]> CaseFoldings { get; private set; }

    /// <summary>
    /// The NFKC_Casefold mapping of the code points whose mapping is not themselves.
    /// An empty sequence means the code point is removed.
    /// </summary>
    internal Dictionary<int, int[]> NfkcCasefold { get; private set; }

    /// <summary>
    /// The Script_Extensions of the code points whose value is not their Script.
    /// </summary>
    internal Dictionary<int, short[]> ScriptExtensions { get; private set; }

    /// <summary>
    /// Reads every file the tables are derived from.
    /// </summary>
    /// <param name="files">The vendored database.</param>
    /// <returns>The properties of every code point.</returns>
    internal static UnicodeCharacterDatabase Read(DatabaseFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var database = new UnicodeCharacterDatabase(PropertyAliases.Read(files));

        database.ReadUnicodeData(files);
        database.ReadSpecialCasing(files);
        database.ReadCaseFolding(files);
        database.ReadNormalizationProperties(files);
        database.ReadBinaryProperties(files, "DerivedCoreProperties.txt");
        database.ReadBinaryProperties(files, "PropList.txt");
        database.ReadEnumeratedProperty(files, "HangulSyllableType.txt", "hst", database.HangulSyllableTypes);
        database.ReadEnumeratedProperty(files, "extracted/DerivedBidiClass.txt", "bc", database.BidiClasses);
        database.ReadEnumeratedProperty(files, "extracted/DerivedJoiningType.txt", "jt", database.JoiningTypes);
        database.ReadScripts(files);
        database.ReadScriptExtensions(files);

        return database;
    }

    private void ReadUnicodeData(DatabaseFiles files)
    {
        var canonical = new Dictionary<int, int[]>();
        var compatibility = new Dictionary<int, int[]>();
        var tags = new Dictionary<int, string>();
        var lowercase = new Dictionary<int, int[]>();
        int opened = -1;

        Array.Fill(DecimalDigits, (sbyte)-1);
        Array.Fill(GeneralCategories, Aliases.Identifier("gc", "Cn"));

        foreach (string[] fields in files.Fields("UnicodeData.txt"))
        {
            int code = CodePoints.Parse(fields[0]);
            bool opening = fields[1].EndsWith(", First>", StringComparison.Ordinal);
            int from = opening || opened < 0 ? code : opened;

            for (int point = from; point <= code; point++)
            {
                GeneralCategories[point] = Aliases.Identifier("gc", fields[2]);
                CombiningClasses[point] = byte.Parse(fields[3], CultureInfo.InvariantCulture);

                if (fields[6].Length > 0)
                {
                    DecimalDigits[point] = sbyte.Parse(fields[6], CultureInfo.InvariantCulture);
                }
            }

            opened = opening ? code : -1;

            ReadDecomposition(code, fields[5], canonical, compatibility, tags);

            if (fields[13].Length > 0)
            {
                lowercase[code] = [CodePoints.Parse(fields[13])];
            }
        }

        CanonicalDecompositions = canonical;
        CompatibilityDecompositions = compatibility;
        CompatibilityTags = tags;
        LowercaseMappings = lowercase;
    }

    private static void ReadDecomposition(
        int code,
        string field,
        Dictionary<int, int[]> canonical,
        Dictionary<int, int[]> compatibility,
        Dictionary<int, string> tags)
    {
        if (field.Length == 0)
        {
            return;
        }

        if (field[0] != '<')
        {
            canonical[code] = CodePoints.Sequence(field);

            return;
        }

        int close = field.IndexOf('>', StringComparison.Ordinal);

        tags[code] = field[1..close];
        compatibility[code] = CodePoints.Sequence(field[(close + 1)..]);
    }

    private void ReadSpecialCasing(DatabaseFiles files)
    {
        foreach (string[] fields in files.Fields("SpecialCasing.txt"))
        {
            // A conditional mapping carries a fifth field naming its condition. What is
            // left after the unconditional ones is the final-sigma rule, which needs the
            // surrounding string and is applied by the case mapper, and three
            // language-sensitive rules, which the default case conversion of Unicode
            // section 3.13 does not apply.
            if (fields.Length > 4 && fields[4].Length > 0)
            {
                continue;
            }

            LowercaseMappings[CodePoints.Parse(fields[0])] = CodePoints.Sequence(fields[1]);
        }
    }

    private void ReadCaseFolding(DatabaseFiles files)
    {
        var folding = new Dictionary<int, int[]>();

        foreach (string[] fields in files.Fields("CaseFolding.txt"))
        {
            // C is the folding the simple and the full operation share, F the further
            // one the full operation applies. S belongs to the simple operation and T
            // to the Turkic one, neither of which this library performs.
            if (fields[1] is "C" or "F")
            {
                folding[CodePoints.Parse(fields[0])] = CodePoints.Sequence(fields[2]);
            }
        }

        CaseFoldings = folding;
    }

    private void ReadNormalizationProperties(DatabaseFiles files)
    {
        var casefold = new Dictionary<int, int[]>();

        foreach (string[] fields in files.Fields("DerivedNormalizationProps.txt"))
        {
            (int first, int last) = CodePoints.Range(fields[0]);

            if (fields[1] is "Full_Composition_Exclusion")
            {
                Array.Fill(FullCompositionExclusions, true, first, last - first + 1);
            }
            else if (fields[1] is "NFKC_CF")
            {
                int[] mapping = fields.Length > 2 ? CodePoints.Sequence(fields[2]) : [];

                for (int point = first; point <= last; point++)
                {
                    casefold[point] = mapping;
                }
            }
        }

        NfkcCasefold = casefold;
    }

    private void ReadBinaryProperties(DatabaseFiles files, string name)
    {
        foreach (string[] fields in files.Fields(name))
        {
            bool[]? property = fields[1] switch
            {
                "Default_Ignorable_Code_Point" => DefaultIgnorables,
                "Cased" => Cased,
                "Case_Ignorable" => CaseIgnorable,
                "Join_Control" => JoinControls,
                "Noncharacter_Code_Point" => Noncharacters,
                _ => null,
            };

            if (property is null)
            {
                continue;
            }

            (int first, int last) = CodePoints.Range(fields[0]);

            Array.Fill(property, true, first, last - first + 1);
        }
    }

    private void ReadEnumeratedProperty(DatabaseFiles files, string name, string property, byte[] values)
    {
        // The default values a file declares in its @missing lines cover the code points
        // it lists no line for, and a later line overrides an earlier default.
        foreach ((int first, int last, string value) in files.Defaults(name))
        {
            Array.Fill(values, Aliases.Identifier(property, value), first, last - first + 1);
        }

        foreach (string[] fields in files.Fields(name))
        {
            (int first, int last) = CodePoints.Range(fields[0]);

            Array.Fill(values, Aliases.Identifier(property, fields[1]), first, last - first + 1);
        }
    }

    private void ReadScripts(DatabaseFiles files)
    {
        Array.Fill(Scripts, Aliases.Script("Unknown"));

        foreach (string[] fields in files.Fields("Scripts.txt"))
        {
            (int first, int last) = CodePoints.Range(fields[0]);

            Array.Fill(Scripts, Aliases.Script(fields[1]), first, last - first + 1);
        }
    }

    private void ReadScriptExtensions(DatabaseFiles files)
    {
        var extensions = new Dictionary<int, short[]>();

        foreach (string[] fields in files.Fields("ScriptExtensions.txt"))
        {
            (int first, int last) = CodePoints.Range(fields[0]);
            string[] codes = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            short[] values = new short[codes.Length];

            for (int index = 0; index < codes.Length; index++)
            {
                values[index] = Aliases.Script(codes[index]);
            }

            Array.Sort(values);

            for (int point = first; point <= last; point++)
            {
                extensions[point] = values;
            }
        }

        ScriptExtensions = extensions;
    }
}
