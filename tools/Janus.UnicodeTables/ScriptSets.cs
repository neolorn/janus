using System;
using System.Collections.Generic;

namespace Janus.UnicodeTables;

/// <summary>
/// The augmented script set of every code point, as UTS #39 section 5.1 defines it.
/// </summary>
/// <remarks>
/// A string is single-script when the intersection of these sets over its characters is
/// not empty. The sets are interned because a few hundred distinct ones cover every code
/// point.
/// </remarks>
internal sealed class ScriptSets
{
    /// <summary>
    /// The identifier of the set of every script, which a character of Common or
    /// Inherited script carries and which therefore never narrows an intersection.
    /// </summary>
    internal const short All = 0;

    private readonly List<short[]> _sets = [[]];
    private readonly Dictionary<string, short> _identifiers = new(StringComparer.Ordinal);

    private ScriptSets()
    {
    }

    /// <summary>
    /// The interned sets, by identifier. The first is the set of every script and is
    /// held as an empty list of members.
    /// </summary>
    internal IReadOnlyList<short[]> Sets => _sets;

    /// <summary>
    /// The set each code point carries.
    /// </summary>
    internal short[] ByCodePoint { get; private set; } = [];

    /// <summary>
    /// Builds the augmented script set of every code point.
    /// </summary>
    /// <param name="database">The properties of every code point.</param>
    /// <returns>The sets and the code points that carry them.</returns>
    internal static ScriptSets Build(UnicodeCharacterDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        var augmentation = new Augmentation(database.Aliases);
        var sets = new ScriptSets();
        short[] byCodePoint = new short[CodePoints.Count];

        for (int code = 0; code < CodePoints.Count; code++)
        {
            short[] extensions = database.ScriptExtensions.TryGetValue(code, out short[]? declared)
                ? declared
                : [database.Scripts[code]];

            byCodePoint[code] = sets.Intern(augmentation.Augment(extensions));
        }

        sets.ByCodePoint = byCodePoint;

        return sets;
    }

    private short Intern(short[]? members)
    {
        if (members is null)
        {
            return All;
        }

        string key = string.Join(',', members);

        if (_identifiers.TryGetValue(key, out short identifier))
        {
            return identifier;
        }

        identifier = checked((short)_sets.Count);
        _sets.Add(members);
        _identifiers[key] = identifier;

        return identifier;
    }

    private sealed class Augmentation
    {
        private readonly short _common;
        private readonly short _inherited;
        private readonly (short Script, short Added)[] _rules;

        internal Augmentation(PropertyAliases aliases)
        {
            _common = aliases.Script("Zyyy");
            _inherited = aliases.Script("Zinh");

            // UTS #39 section 5.1: a set naming a script of a multi-script writing
            // system also names that writing system.
            _rules =
            [
                (aliases.Script("Hani"), aliases.Script("Hanb")),
                (aliases.Script("Hani"), aliases.Script("Hntl")),
                (aliases.Script("Hani"), aliases.Script("Jpan")),
                (aliases.Script("Hani"), aliases.Script("Kore")),
                (aliases.Script("Hira"), aliases.Script("Jpan")),
                (aliases.Script("Kana"), aliases.Script("Jpan")),
                (aliases.Script("Hang"), aliases.Script("Kore")),
                (aliases.Script("Bopo"), aliases.Script("Hanb")),
                (aliases.Script("Latn"), aliases.Script("Hntl")),
            ];
        }

        internal short[]? Augment(short[] extensions)
        {
            if (Array.IndexOf(extensions, _common) >= 0 || Array.IndexOf(extensions, _inherited) >= 0)
            {
                return null;
            }

            var augmented = new SortedSet<short>(extensions);

            foreach ((short script, short added) in _rules)
            {
                if (Array.IndexOf(extensions, script) >= 0)
                {
                    augmented.Add(added);
                }
            }

            return [.. augmented];
        }
    }
}
