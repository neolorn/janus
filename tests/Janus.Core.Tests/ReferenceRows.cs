using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Janus.Core.Tests;

/// <summary>
/// The rows chapter 10 holds and the rows the ledger owes it, read from the two
/// documents as they stand (REF-001): the first backticked name in the first cell of
/// every live row of the tables one section holds.
/// </summary>
internal static class ReferenceRows
{
    private const string Reference = "docs/spec/10-reference.md";

    private const string Ledger = "docs/reports/decisions-pending-review.md";

    // Where the ledger lists what chapter 10 does not hold yet. Everything above it
    // is decisions, whose tables are not rows of the chapter.
    private const string Owing = "# Rows for chapter 10";

    // The first backticked name in a cell, whatever marks follow it.
    private static readonly Regex Name = new(
        "`([^`]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    // What a family member is written with in place of its last segments, such as
    // `<organization>` or `<host-category>`, read as one placeholder whatever it says.
    private static readonly Regex Placeholder = new(
        "<[^>]*>",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    // A scope cell marking its key P, whatever qualifies the mark after it.
    private static readonly Regex ProtectedMark = new(
        "^P\\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// The codes chapter 10 section 1 holds a live row for.
    /// </summary>
    public static IReadOnlySet<string> ChapterCodes { get; } =
        Names(Rows(Section(Lines(Reference), "## 1. "), "Code"));

    /// <summary>
    /// The codes the ledger's section 1 owes chapter 10.
    /// </summary>
    public static IReadOnlySet<string> OwedCodes { get; } =
        Names(Rows(Section(Owed(), "## Section 1, "), "Code"));

    /// <summary>
    /// The keys chapter 10 section 4 holds a live row for, a family written as its
    /// prefix and one placeholder.
    /// </summary>
    public static IReadOnlySet<string> ChapterKeys { get; } =
        Names(Rows(Section(Lines(Reference), "## 4. "), "Key"));

    /// <summary>
    /// The keys the ledger's section 4 owes chapter 10, a family written as its prefix
    /// and one placeholder.
    /// </summary>
    public static IReadOnlySet<string> OwedKeys { get; } =
        Names(Rows(Section(Owed(), "## Section 4, "), "Key"));

    /// <summary>
    /// The keys whose row, in chapter 10 section 4 or among the rows the ledger owes
    /// it, marks them P in its scope column.
    /// </summary>
    public static IReadOnlySet<string> MarkedProtected { get; } =
        Names(Rows(Section(Lines(Reference), "## 4. "), "Key")
            .Concat(Rows(Section(Owed(), "## Section 4, "), "Key"))
            .Where(Protected));

    /// <summary>
    /// How a family of keys is written in the rows, so the catalogue's prefix can be
    /// compared with them.
    /// </summary>
    /// <param name="prefix">The family's prefix.</param>
    /// <returns>The prefix followed by one placeholder segment.</returns>
    public static string Family(string prefix) => prefix + ".<>";

    private static string[] Lines(string relativePath) =>
        Repository.ReadText(relativePath).ReplaceLineEndings("\n").Split('\n');

    private static string[] Owed() =>
    [
        .. Lines(Ledger)
            .SkipWhile(line => !string.Equals(line.TrimEnd(), Owing, StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("# ", StringComparison.Ordinal)),
    ];

    // The lines under the level-two heading opening with the text given, up to the
    // next heading of level one or two. A missing heading is a failure, since a
    // section that moved would otherwise read as one holding nothing.
    private static string[] Section(string[] lines, string heading)
    {
        string[] section =
        [
            .. lines
                .SkipWhile(line => !line.StartsWith(heading, StringComparison.Ordinal))
                .Skip(1)
                .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)
                    && !line.StartsWith("# ", StringComparison.Ordinal)),
        ];

        if (section.Length == 0)
        {
            throw new InvalidOperationException("No section opens with \"" + heading + "\".");
        }

        return section;
    }

    // The live rows of the tables whose header opens with the column given, each with
    // its name, its table's header and its cells. A table of any other header, such
    // as the fields of the policy object, is passed over.
    private static IEnumerable<(string Name, string[] Header, string[] Cells)> Rows(
        string[] lines,
        string column)
    {
        string[]? header = null;

        foreach (string line in lines.Select(line => line.Trim()))
        {
            if (!line.StartsWith('|'))
            {
                header = null;

                continue;
            }

            string[] cells = Cells(line);

            if (header is null)
            {
                header = cells;

                continue;
            }

            if (!string.Equals(header[0], column, StringComparison.Ordinal)
                || cells.All(cell => cell.Trim(':', '-').Length == 0)
                || Retired(cells))
            {
                continue;
            }

            Match named = Name.Match(cells[0]);

            if (named.Success)
            {
                yield return (Placeholder.Replace(named.Groups[1].Value, "<>"), header, cells);
            }
        }
    }

    private static HashSet<string> Names(IEnumerable<(string Name, string[] Header, string[] Cells)> rows) =>
        rows.Select(row => row.Name).ToHashSet(StringComparer.Ordinal);

    private static string[] Cells(string line) =>
        [.. line.Trim('|').Split('|').Select(cell => cell.Trim())];

    // A row struck through, or one whose second cell says the code or key is retired
    // or withdrawn, names something the chapter no longer holds.
    private static bool Retired(string[] cells) =>
        cells[0].StartsWith("~~", StringComparison.Ordinal)
        || (cells.Length > 1
            && (cells[1].TrimStart('*', ' ').StartsWith("Retired", StringComparison.Ordinal)
                || cells[1].TrimStart('*', ' ').StartsWith("Withdrawn", StringComparison.Ordinal)));

    private static bool Protected((string Name, string[] Header, string[] Cells) row)
    {
        int scope = Array.IndexOf(row.Header, "Scope");

        return scope >= 0
            && scope < row.Cells.Length
            && ProtectedMark.IsMatch(row.Cells[scope].Replace("*", string.Empty, StringComparison.Ordinal).Trim());
    }
}
