using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The statements a block of a migration's script runs, each reported as itself
/// (OPS-MIG-001, D-206).
/// </summary>
[Trait("kind", "unit")]
public sealed class ScriptStatementsTests
{
    /// <summary>
    /// OPS-MIG-001 AC5: a block holding several statements is cut at each semicolon that
    /// ends one, the indentation the script gives the block is taken off, and a
    /// statement's own indentation and line breaks stand, joined by line feeds.
    /// </summary>
    [Fact]
    public void OPS_MIG_001_AC5_ABlockIsCutIntoItsStatementsWithoutTheScriptsIndentation()
    {
        string block = "\r\n    CREATE INDEX ix_one\r\n        ON identity.one (a);\r\n    DROP INDEX identity.ix_two;\r\n";

        Assert.Equal(
            ["CREATE INDEX ix_one\n    ON identity.one (a)", "DROP INDEX identity.ix_two"],
            ScriptStatements.Of(block));
    }

    /// <summary>
    /// OPS-MIG-001 AC5: a semicolon inside a quoted literal, a quoted identifier, a
    /// dollar-quoted body, tagged or not, or a comment ends no statement, so a function
    /// or an anonymous block written by hand is one statement.
    /// </summary>
    /// <param name="statement">A statement holding a semicolon that ends nothing.</param>
    [Theory]
    [InlineData("INSERT INTO t (a) VALUES ('x;y'), ('it''s;')")]
    [InlineData("ALTER TABLE \"odd;name\" ADD c integer")]
    [InlineData("CREATE FUNCTION f() RETURNS void LANGUAGE plpgsql AS $function$ BEGIN DELETE FROM t; DROP TABLE u; END $function$")]
    [InlineData("DO $$ BEGIN PERFORM 1; END $$")]
    [InlineData("SELECT 1 /* one; /* two; */ three; */ + 2")]
    [InlineData("SELECT 1 -- a comment; still a comment\n+ 2")]
    [InlineData("SELECT a$b$c FROM t WHERE x = $1")]
    public void OPS_MIG_001_AC5_ASemicolonThatEndsNoStatementCutsNothing(string statement) =>
        Assert.Equal([statement, "SELECT 2"], ScriptStatements.Of(statement + ";\nSELECT 2;"));

    /// <summary>
    /// OPS-MIG-001 AC5: a block that holds nothing but blanks and semicolons holds no
    /// statement.
    /// </summary>
    [Fact]
    public void OPS_MIG_001_AC5_ABlockOfBlanksHoldsNoStatement() =>
        Assert.Empty(ScriptStatements.Of("\n    ;\n   ;  \n"));
}
