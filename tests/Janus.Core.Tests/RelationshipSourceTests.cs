using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Janus.Core.Tests;

/// <summary>
/// The source a host declares for one relationship's rows (LIB-HOST-001,
/// AUTHZ-DERIVE-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class RelationshipSourceTests
{
    /// <summary>
    /// LIB-HOST-001: a source names its relationship, the context its rows are read
    /// from and the row type it answers, and hands the rows of one context instance to
    /// a reader under that type.
    /// </summary>
    [Fact]
    public void LIB_HOST_001_ASourceHandsTheRowsOfOneContextInstanceToItsReader()
    {
        var context = new Ledger([new Keeper("shelf-1"), new Keeper("shelf-2")]);
        var reader = new Reader();

        var source = RelationshipSource.Of<Ledger, Keeper>("keeper", ledger => ledger.Keepers);

        source.Read(context, reader);

        Assert.Equal("keeper", source.Relationship);
        Assert.Equal(typeof(Ledger), source.Context);
        Assert.Equal(typeof(Keeper), source.Row);
        Assert.Equal(typeof(Keeper), reader.Row);
        Assert.Equal(["shelf-1", "shelf-2"], reader.Read.Cast<Keeper>().Select(keeper => keeper.Shelf));
    }

    /// <summary>
    /// LIB-HOST-001: a source with no relationship, or no rows, is not made.
    /// </summary>
    /// <param name="relationship">A name that names nothing.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LIB_HOST_001_ASourceNamingNoRelationshipOrNoRowsIsNotMade(string relationship)
    {
        Assert.Throws<ArgumentException>(
            () => RelationshipSource.Of<Ledger, Keeper>(relationship, ledger => ledger.Keepers));
        Assert.Throws<ArgumentNullException>(
            () => RelationshipSource.Of<Ledger, Keeper>("keeper", null!));
    }

    // A host's context as small as one can be: the rows of one relationship.
    private sealed class Ledger(IReadOnlyList<Keeper> keepers)
    {
        public IQueryable<Keeper> Keepers => keepers.AsQueryable();
    }

    private sealed record Keeper(string Shelf);

    // A reader that keeps what it was handed and the type it was handed it under.
    private sealed class Reader : IRelationshipRowsReader
    {
        public Type? Row { get; private set; }

        public IReadOnlyList<object> Read { get; private set; } = [];

        void IRelationshipRowsReader.Read<TRow>(IQueryable<TRow> rows)
        {
            Row = typeof(TRow);
            Read = [.. rows.Cast<object>()];
        }
    }
}
