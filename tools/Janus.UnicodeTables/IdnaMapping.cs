using System;
using System.Collections.Generic;
using System.Globalization;

namespace Janus.UnicodeTables;

/// <summary>
/// The IDNA mapping table of UTS #46 section 5: the status of every code point, and the
/// replacement of each one whose status is mapped.
/// </summary>
/// <remarks>
/// Serves REG-DOM-001. The table's third status column, which says what IDNA2008 makes
/// of a code point UTS #46 holds valid, is read by no step of the processing and is not
/// kept. A deviation's replacement belongs to transitional processing, which the library
/// does not perform, and is not kept either.
/// </remarks>
internal sealed class IdnaMapping
{
    /// <summary>
    /// The name of the vendored table.
    /// </summary>
    internal const string Table = "IdnaMappingTable.txt";

    private const string Mapped = "Mapped";

    private const string Disallowed = "Disallowed";

    private IdnaMapping()
    {
        Statuses = new byte[CodePoints.Count];
        Replacements = [];
    }

    /// <summary>
    /// The status values, in the order their identifiers are numbered.
    /// </summary>
    internal static IReadOnlyList<string> Values { get; } =
        ["Deviation", Disallowed, "Ignored", Mapped, "Valid"];

    /// <summary>
    /// The status of each code point.
    /// </summary>
    internal byte[] Statuses { get; }

    /// <summary>
    /// The replacement of each code point whose status is mapped.
    /// </summary>
    internal Dictionary<int, int[]> Replacements { get; }

    /// <summary>
    /// Reads the table.
    /// </summary>
    /// <param name="files">The vendored database.</param>
    /// <returns>The status and the replacement of every code point.</returns>
    internal static IdnaMapping Read(DatabaseFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var mapping = new IdnaMapping();

        // A code point the table gave no line would be one the standard said nothing
        // of, and the processing refuses what it has no status for.
        Array.Fill(mapping.Statuses, Identifier(Disallowed));

        foreach (string[] fields in files.Fields(Table))
        {
            (int first, int last) = CodePoints.Range(fields[0]);
            string status = char.ToUpperInvariant(fields[1][0]) + fields[1][1..];

            Array.Fill(mapping.Statuses, Identifier(status), first, last - first + 1);

            if (status != Mapped)
            {
                continue;
            }

            int[] replacement = CodePoints.Sequence(fields[2]);

            for (int point = first; point <= last; point++)
            {
                mapping.Replacements[point] = replacement;
            }
        }

        return mapping;
    }

    private static byte Identifier(string status)
    {
        for (int index = 0; index < Values.Count; index++)
        {
            if (Values[index] == status)
            {
                return (byte)index;
            }
        }

        throw new InvalidOperationException(string.Create(
            CultureInfo.InvariantCulture,
            $"{Table} gives the status {status}, which UTS #46 section 5 does not name."));
    }
}
