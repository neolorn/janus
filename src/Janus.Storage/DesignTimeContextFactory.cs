using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Janus.Storage;

/// <summary>
/// How the migration tooling builds the context. Nothing connects: the tooling reads
/// the model and writes the migration, and the pipeline applies it under the migration
/// credential against the database it was given.
/// </summary>
/// <remarks>Implements OPS-MIG-001 and CONV-DESIGN-003.</remarks>
internal sealed class DesignTimeContextFactory : IDesignTimeDbContextFactory<StoreContext>
{
    /// <inheritdoc/>
    public StoreContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<StoreContext>()
            .UseNpgsql(
                "Host=design-time;Database=janus",
                npgsql => npgsql.MigrationsHistoryTable(
                    StoreContext.MigrationsHistoryTable,
                    StoreContext.Schema))
            .Options);
}
