using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// What the one context maps (CONV-DESIGN-003).
/// </summary>
/// <remarks>
/// The model is built from the design-time factory, which connects to nothing, so this
/// reads the mapping and not a database.
/// </remarks>
[Trait("kind", "unit")]
public sealed class ModelTests
{
    /// <summary>
    /// CONV-DESIGN-003 AC4: no domain entity type appears in the model. What is mapped
    /// is a persistence record, declared in this project beside its configuration, so
    /// an aggregate can never be loaded, tracked or written by the context itself.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_AC4_NoDomainEntityTypeAppearsInTheModel()
    {
        IEnumerable<Type> mapped = Model().GetEntityTypes().Select(entity => entity.ClrType);

        Assert.NotEmpty(mapped);
        Assert.All(mapped, type => Assert.Equal(typeof(JanusDbContext).Assembly, type.Assembly));
    }

    /// <summary>
    /// CONV-DESIGN-003: every table the library owns is mapped in the schema the library
    /// owns, so nothing of the host's is ever read or written through this context.
    /// </summary>
    [Fact]
    public void CONV_DESIGN_003_EveryMappedTableIsInTheLibrarysOwnSchema() =>
        Assert.All(
            Model().GetEntityTypes(),
            entity => Assert.Equal(JanusDbContext.Schema, entity.GetSchema()));

    private static IModel Model()
    {
        using JanusDbContext context = new DesignTimeContextFactory().CreateDbContext([]);

        return context.Model;
    }
}
