using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Janus.Storage;

/// <summary>
/// Stores a fixed enumeration of chapter 10 section 5 under the spelling the chapter
/// gives it, and lists those spellings for the column's check constraint.
/// </summary>
/// <typeparam name="TVocabulary">The enumeration.</typeparam>
/// <remarks>
/// Implements CONV-ENUM-001. The spelling comes from the member's own wire name, so
/// the schema and the wire cannot drift apart. Reading it is the reflection
/// CONV-CODE-004 leaves to the model builder, and it happens once, at startup.
/// </remarks>
internal sealed class VocabularyConverter<TVocabulary> : ValueConverter<TVocabulary, string>
    where TVocabulary : struct, Enum
{
    private static readonly FrozenDictionary<TVocabulary, string> Spellings = Enum
        .GetValues<TVocabulary>()
        .ToFrozenDictionary(member => member, Spelling);

    private static readonly FrozenDictionary<string, TVocabulary> Members = Spellings
        .ToFrozenDictionary(entry => entry.Value, entry => entry.Key, StringComparer.Ordinal);

    /// <summary>
    /// Builds the converter over the enumeration's own wire names.
    /// </summary>
    public VocabularyConverter()
        : base(member => ToSpelling(member), spelling => ToMember(spelling))
    {
    }

    /// <summary>
    /// The spellings the column admits, in order, for its check constraint.
    /// </summary>
    public static IReadOnlyList<string> Admitted { get; } =
        [.. Spellings.Values.Order(StringComparer.Ordinal)];

    /// <summary>
    /// The member a stored spelling stands for.
    /// </summary>
    /// <param name="spelling">The spelling as the column holds it.</param>
    /// <returns>The member.</returns>
    /// <exception cref="KeyNotFoundException">
    /// The spelling is outside the vocabulary, which the column's check refuses.
    /// </exception>
    public static TVocabulary Read(string spelling) => Members[spelling];

    /// <summary>
    /// The spelling a member is held under.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The spelling as the column holds it.</returns>
    public static string Write(TVocabulary member) => Spellings[member];

    private static string ToSpelling(TVocabulary member) => Spellings[member];

    private static TVocabulary ToMember(string spelling) => Members[spelling];

    private static string Spelling(TVocabulary member) =>
        typeof(TVocabulary)
            .GetField(member.ToString())!
            .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!
            .Name;
}
