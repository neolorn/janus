using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Janus.Cli.Migration;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// The destructive-operation report <c>migrate --report</c> writes: one JSON line for
/// each statement the scan matches, marked destructive exactly where OPS-DEP-001 names it
/// (OPS-MIG-001, OPS-DEP-001, CONV-DESIGN-003, D-206).
/// </summary>
[Trait("kind", "unit")]
public sealed class DestructiveOperationReportTests
{
    private const string First = "20260101000000_First";

    private const string Second = "20260102000000_Second";

    /// <summary>
    /// OPS-MIG-001 AC5: a statement that loses data is destructive whatever the table,
    /// and one that adds what can fail against rows is destructive on a table the
    /// pending migrations neither created nor emptied of rows, a table they created and
    /// then filled holding rows as a deployed one does.
    /// </summary>
    /// <param name="statement">The statement judged on a table already deployed.</param>
    [Theory]
    [InlineData("DROP TABLE identity.held")]
    [InlineData("ALTER TABLE identity.held DROP COLUMN settles_at")]
    [InlineData("DROP SCHEMA archive CASCADE")]
    [InlineData("ALTER TABLE identity.held ALTER COLUMN kind TYPE smallint")]
    [InlineData("TRUNCATE identity.held")]
    [InlineData("DELETE FROM identity.held WHERE kind = 'old'")]
    [InlineData("ALTER TABLE identity.held ADD CONSTRAINT ck_held_kind CHECK (kind <> '')")]
    [InlineData("ALTER TABLE identity.held ADD CONSTRAINT fk_held_owner FOREIGN KEY (owner) REFERENCES identity.owners (id)")]
    [InlineData("ALTER TABLE identity.held ALTER COLUMN kind SET NOT NULL")]
    [InlineData("CREATE UNIQUE INDEX ux_held_kind ON identity.held (kind)")]
    [InlineData("ALTER TABLE identity.held ADD owner uuid NOT NULL")]
    public void OPS_MIG_001_AC5_WhatOpsDep001NamesIsDestructive(string statement) =>
        Assert.Equal([(First, statement, true)], Lines([(First, statement)]));

    /// <summary>
    /// OPS-MIG-001 AC5: an added constraint on a table the pending migrations created is
    /// reported and not destructive, and so is a drop that loses no data; on a table they
    /// created and then filled the constraint is destructive.
    /// </summary>
    [Fact]
    public void OPS_MIG_001_AC5_AConstraintOnACreatedTableIsReportedAndNotDestructive()
    {
        const string created = "CREATE TABLE identity.fresh (id uuid NOT NULL, kind text)";
        const string constrained = "ALTER TABLE identity.fresh ADD CONSTRAINT pk_fresh PRIMARY KEY (id)";
        const string filled = "INSERT INTO identity.fresh (id, kind) VALUES (gen_random_uuid(), 'a')";
        const string checkedAfter = "ALTER TABLE identity.fresh ADD CONSTRAINT ck_fresh_kind CHECK (kind <> '')";
        const string dropped = "DROP INDEX identity.ix_fresh_kind";

        Assert.Equal(
            [(First, constrained, false), (Second, dropped, false), (Second, checkedAfter, true)],
            Lines(
            [
                (First, created),
                (First, constrained),
                (Second, dropped),
                (Second, filled),
                (Second, checkedAfter),
            ]));
    }

    /// <summary>
    /// OPS-MIG-001 AC5: what the scan does not match, and what it matches only on a
    /// table already deployed when the table is one the pending migrations created, is
    /// not written; where nothing matches, nothing is.
    /// </summary>
    [Fact]
    public void OPS_MIG_001_AC5_NothingIsWrittenWhereNothingMatches()
    {
        string report = DestructiveOperationReport.Of(
            [
                (First, "CREATE TABLE identity.fresh (id uuid NOT NULL)"),
                (First, "ALTER TABLE identity.fresh ADD kind text NOT NULL"),
                (First, "ALTER TABLE identity.fresh ALTER COLUMN kind SET NOT NULL"),
                (First, "CREATE UNIQUE INDEX ux_fresh_kind ON identity.fresh (kind)"),
                (Second, "CREATE INDEX ix_held_kind ON identity.held (kind)"),
                (Second, "ALTER TABLE identity.held ADD note text"),
                (Second, "ALTER TABLE identity.held ADD rank integer NOT NULL DEFAULT 0"),
                (Second, "INSERT INTO identity.settings (key) VALUES ('a')"),
            ],
            "\n");

        Assert.Empty(report);
        Assert.Empty(DestructiveOperationReport.Of([], "\n"));
    }

    /// <summary>
    /// OPS-MIG-001 AC5: each line is one JSON object of the migration's identifier, the
    /// statement itself and whether it is destructive, and the lines follow the order the
    /// statements apply in.
    /// </summary>
    [Fact]
    public void OPS_MIG_001_AC5_EachLineIsTheMigrationTheStatementAndWhetherItIsDestructive()
    {
        string report = DestructiveOperationReport.Of(
            [
                (First, "DROP INDEX identity.ix_one"),
                (Second, "ALTER TABLE identity.held DROP COLUMN \"it's\""),
            ],
            "\r\n");

        string[] lines = report.Split("\r\n");

        Assert.Equal(2, lines.Length);
        Assert.All(lines, line =>
        {
            using var parsed = JsonDocument.Parse(line);

            Assert.Equal(
                ["migration", "statement", "destructive"],
                parsed.RootElement.EnumerateObject().Select(member => member.Name));
        });
        Assert.Equal(
            [(First, "DROP INDEX identity.ix_one", false), (Second, "ALTER TABLE identity.held DROP COLUMN \"it's\"", true)],
            Lines(
            [
                (First, "DROP INDEX identity.ix_one"),
                (Second, "ALTER TABLE identity.held DROP COLUMN \"it's\""),
            ]));
    }

    private static List<(string Migration, string Statement, bool Destructive)> Lines(IReadOnlyList<(string Migration, string Statement)> statements)
    {
        string report = DestructiveOperationReport.Of(statements, "\n");

        return report.Length is 0
            ? []
            : [.. report.Split('\n').Select(line =>
            {
                using var parsed = JsonDocument.Parse(line);
                JsonElement root = parsed.RootElement;

                return (
                    root.GetProperty("migration").GetString()!,
                    root.GetProperty("statement").GetString()!,
                    root.GetProperty("destructive").GetBoolean());
            })];
    }
}
