using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Janus.Storage;

/// <summary>
/// How the migration tooling builds the context. Nothing connects: the tooling reads
/// the model and writes the migration, and the pipeline applies it under the migration
/// credential against the database it was given.
/// </summary>
/// <remarks>Implements OPS-MIG-001 and CONV-DESIGN-003.</remarks>
internal sealed class DesignTimeContextFactory : IDesignTimeDbContextFactory<JanusDbContext>
{
    /// <inheritdoc/>
    public JanusDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<JanusDbContext>()
            .UseNpgsql(
                "Host=design-time;Database=janus",
                npgsql => npgsql.MigrationsHistoryTable(
                    JanusDbContext.MigrationsHistoryTable,
                    JanusDbContext.Schema))
            .Options);
}
