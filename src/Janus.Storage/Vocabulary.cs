using System;
using System.Collections.Generic;

namespace Janus.Storage;

/// <summary>
/// The check a constrained text column carries in place of a native enumeration type.
/// </summary>
/// <remarks>
/// Implements CONV-ENUM-001. The spellings come from the enumeration's own wire names,
/// so a value the code does not branch on is refused by the database and the admitted
/// set changes without a locking migration.
/// </remarks>
internal static class Vocabulary
{
    /// <summary>
    /// The check expression admitting exactly an enumeration's spellings.
    /// </summary>
    /// <typeparam name="TVocabulary">The enumeration.</typeparam>
    /// <param name="column">The column it constrains.</param>
    /// <returns>The expression.</returns>
    public static string Admits<TVocabulary>(string column)
        where TVocabulary : struct, Enum =>
        Admits(column, VocabularyConverter<TVocabulary>.Admitted);

    /// <summary>
    /// The check expression admitting exactly the spellings given.
    /// </summary>
    /// <param name="column">The column it constrains.</param>
    /// <param name="spellings">The spellings.</param>
    /// <returns>The expression.</returns>
    public static string Admits(string column, IReadOnlyList<string> spellings) =>
        column + " IN ('" + string.Join("', '", spellings) + "')";
}
