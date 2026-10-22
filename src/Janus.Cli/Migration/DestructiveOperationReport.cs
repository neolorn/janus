using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Janus.Cli.Migration;

/// <summary>
/// The destructive-operation report of the migrations pending on a database: each
/// statement the scan matches, marked destructive where it loses data or adds what can
/// fail against rows a table already holds.
/// </summary>
/// <remarks>
/// Implements OPS-MIG-001 AC5, OPS-DEP-001, OPS-DEP-002 and CONV-DESIGN-003, as D-206
/// settles them. The scan is the one the library's own pipeline runs over the script it
/// writes: <c>DROP</c>, <c>ALTER ... TYPE</c>, an added constraint, <c>TRUNCATE</c> and
/// <c>DELETE FROM</c>, and, on a table the same migrations did not create or have
/// filled, <c>SET NOT NULL</c>, a unique index and a column added <c>NOT NULL</c>
/// without a default. Every statement is judged in the order the migrations apply, so a
/// table they create is known from its creation on, and one they fill holds rows from
/// that statement on. Each match is one JSON line, and a statement the scan does not
/// match is not written.
/// </remarks>
internal static partial class DestructiveOperationReport
{
    /// <summary>
    /// The lines of the report.
    /// </summary>
    /// <param name="statements">The statements of the pending migrations, each with its migration, in the order they apply.</param>
    /// <param name="newLine">What ends a line on standard output.</param>
    /// <returns>One JSON object a line, or nothing where no statement matches.</returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static string Of(IReadOnlyList<(string Migration, string Statement)> statements, string newLine)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ArgumentNullException.ThrowIfNull(newLine);

        var created = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>();

        foreach ((string Migration, string Statement) statement in statements)
        {
            if (Judged(statement.Statement, created) is bool destructive)
            {
                lines.Add(Line(statement, destructive));
            }
        }

        return string.Join(newLine, lines);
    }

    // Whether the scan matches a statement, and whether what it matched is destructive;
    // nothing where it does not match. The tables the migrations create, less those
    // they fill, are kept as the statements are judged.
    private static bool? Judged(string statement, HashSet<string> created)
    {
        string piece = Whitespace().Replace(statement, " ").Trim();
        string upper = piece.ToUpperInvariant();
        string table = string.Empty;

        if (CreateTable().Match(upper) is { Success: true } creation)
        {
            _ = created.Add(Named(creation.Value));
        }

        // A table the same migrations fill holds rows again, as one already deployed does.
        if (InsertInto().Match(upper) is { Success: true } insertion)
        {
            _ = created.Remove(Named(insertion.Value));
        }

        if (AlterTable().Match(upper) is { Success: true } alteration)
        {
            table = Named(alteration.Value);
        }
        else if (CreateUniqueIndex().IsMatch(upper) && IndexedTable().Match(upper) is { Success: true } indexed)
        {
            table = Named(indexed.Value);
        }

        bool existing = table.Length is not 0 && !created.Contains(table);

        // Data lost, whatever the table.
        if (DataLost().IsMatch(upper) || AlteredType().IsMatch(upper) || Truncated().IsMatch(upper) || Deleted().IsMatch(upper))
        {
            return true;
        }

        // A constraint, which can fail only against rows a table already holds.
        if (AddedConstraint().IsMatch(upper))
        {
            return existing;
        }

        if (existing
            && (SetNotNull().IsMatch(upper)
                || CreateUniqueIndex().IsMatch(upper)
                || (Added().IsMatch(upper) && NotNull().IsMatch(upper) && !Defaulted().IsMatch(upper))))
        {
            return true;
        }

        return Dropped().IsMatch(upper) ? false : null;
    }

    // The last word of what a pattern matched, its quotes taken off.
    private static string Named(string matched) =>
        matched.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1].Replace("\"", string.Empty, StringComparison.Ordinal);

    private static string Line((string Migration, string Statement) statement, bool destructive)
    {
        using var buffer = new MemoryStream();

        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("migration", statement.Migration);
            json.WriteString("statement", statement.Statement);
            json.WriteBoolean("destructive", destructive);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Whitespace();

    [GeneratedRegex("^ *CREATE (UNLOGGED |TEMP |TEMPORARY )?TABLE (IF NOT EXISTS )?[^ (]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CreateTable();

    [GeneratedRegex("^ *INSERT INTO [^ (]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex InsertInto();

    [GeneratedRegex("^ *ALTER TABLE (IF EXISTS )?(ONLY )?[^ ]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AlterTable();

    [GeneratedRegex("^ *CREATE UNIQUE INDEX ", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CreateUniqueIndex();

    [GeneratedRegex(" ON (ONLY )?[^ (]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex IndexedTable();

    [GeneratedRegex("(^|[^A-Z0-9_])DROP +(TABLE|COLUMN|SCHEMA)([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex DataLost();

    [GeneratedRegex("(^|[^A-Z0-9_])ALTER[^A-Z0-9_](.*[^A-Z0-9_])?TYPE([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AlteredType();

    [GeneratedRegex("(^|[^A-Z0-9_])TRUNCATE([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Truncated();

    [GeneratedRegex("(^|[^A-Z0-9_])DELETE +FROM([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Deleted();

    [GeneratedRegex("(^|[^A-Z0-9_])ADD +(CONSTRAINT|PRIMARY +KEY|UNIQUE|CHECK|FOREIGN +KEY|EXCLUDE)([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex AddedConstraint();

    [GeneratedRegex("(^|[^A-Z0-9_])SET +NOT +NULL([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex SetNotNull();

    [GeneratedRegex("(^|[^A-Z0-9_])ADD([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Added();

    [GeneratedRegex("NOT +NULL", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex NotNull();

    [GeneratedRegex("(^|[^A-Z0-9_])(DEFAULT|GENERATED)([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Defaulted();

    [GeneratedRegex("(^|[^A-Z0-9_])DROP([^A-Z0-9_]|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Dropped();
}
