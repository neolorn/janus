using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Janus.UnicodeTables;

/// <summary>
/// The values each enumerated property takes, and the identifier the tables hold for
/// each of them.
/// </summary>
/// <remarks>
/// The database spells one value in several ways: <c>Scripts.txt</c> writes
/// <c>Latin</c> where <c>ScriptExtensions.txt</c> writes <c>Latn</c>. Every spelling
/// resolves here to the one identifier, and the identifier is the value's position in
/// the order <c>PropertyValueAliases.txt</c> lists it, so a table regenerated from the
/// same files holds the same numbers.
/// </remarks>
internal sealed class PropertyAliases
{
    /// <summary>
    /// The property abbreviation of General_Category.
    /// </summary>
    internal const string GeneralCategoryProperty = "gc";

    /// <summary>
    /// The property abbreviation of Script.
    /// </summary>
    internal const string ScriptProperty = "sc";

    // UAX #44 lists a group of General_Category values, such as Other for the five
    // categories beginning with C, beside the values themselves. A code point never
    // carries a group, so the tables do not name one.
    private static readonly string[] Groupings = ["C", "L", "LC", "M", "N", "P", "S", "Z"];

    // UTS #39 section 5.1 augments a character's script set with the writing systems
    // that span several scripts. They are not Script values, so the database does not
    // list them and the tables add them after the ones it does.
    private static readonly string[] WritingSystems = ["Hanb", "Hntl", "Jpan", "Kore"];

    private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> _identifiers = new(StringComparer.Ordinal);

    private PropertyAliases()
    {
    }

    /// <summary>
    /// Reads the alias file and adds the writing systems UTS #39 needs.
    /// </summary>
    /// <param name="files">The vendored database.</param>
    /// <returns>The values of every enumerated property.</returns>
    internal static PropertyAliases Read(DatabaseFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var aliases = new PropertyAliases();

        foreach (string[] fields in files.Fields("PropertyValueAliases.txt"))
        {
            if (fields.Length < 3 || IsGrouping(fields))
            {
                continue;
            }

            aliases.Add(fields[0], fields[1], fields[2..]);
        }

        foreach (string system in WritingSystems)
        {
            aliases.Add(ScriptProperty, system, []);
        }

        return aliases;
    }

    /// <summary>
    /// The identifier of a property value, whichever way it is spelled.
    /// </summary>
    /// <param name="property">The property abbreviation.</param>
    /// <param name="value">The value, in any of its spellings.</param>
    /// <returns>The identifier the tables hold for it.</returns>
    internal byte Identifier(string property, string value) => checked((byte)Resolve(property, value));

    /// <summary>
    /// The identifier of a script, whichever way it is spelled.
    /// </summary>
    /// <param name="value">The script, in any of its spellings.</param>
    /// <returns>The identifier the tables hold for it.</returns>
    internal short Script(string value) => checked((short)Resolve(ScriptProperty, value));

    /// <summary>
    /// The values of a property, in identifier order.
    /// </summary>
    /// <param name="property">The property abbreviation.</param>
    /// <returns>The short name of each value.</returns>
    internal IReadOnlyList<string> Values(string property) =>
        new ReadOnlyCollection<string>(_values[property]);

    private static bool IsGrouping(string[] fields) =>
        string.Equals(fields[0], GeneralCategoryProperty, StringComparison.Ordinal)
        && Array.IndexOf(Groupings, fields[1]) >= 0;

    private void Add(string property, string value, string[] alternatives)
    {
        if (!_values.TryGetValue(property, out List<string>? values))
        {
            values = [];
            _values[property] = values;
            _identifiers[property] = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        int identifier = values.Count;

        values.Add(value);
        _identifiers[property][value] = identifier;

        foreach (string alternative in alternatives)
        {
            if (alternative.Length > 0 && !string.Equals(alternative, "n/a", StringComparison.Ordinal))
            {
                _identifiers[property][alternative] = identifier;
            }
        }
    }

    private int Resolve(string property, string value)
    {
        if (_identifiers.TryGetValue(property, out Dictionary<string, int>? identifiers)
            && identifiers.TryGetValue(value, out int identifier))
        {
            return identifier;
        }

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"{property} has no value {value} in the vendored alias file."));
    }
}
