using System;
using Janus.Core;
using Janus.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A host's own context, as small as a host can be: one table of its own, and the two
/// tables a permission filter reads mapped into it.
/// </summary>
/// <param name="options">Where the context reaches its database.</param>
/// <remarks>
/// This is the host of LIB-HOST-002: the library owns neither the table nor the query,
/// and the filter is applied by the host to its own query.
/// </remarks>
internal sealed class HostContext(DbContextOptions<HostContext> options) : DbContext(options)
{
    /// <summary>
    /// The host's own records.
    /// </summary>
    public DbSet<HostDocument> Documents => Set<HostDocument>();

    /// <summary>
    /// The ancestry closure, read from the library's schema.
    /// </summary>
    public DbSet<AncestryEntry> Ancestry => Set<AncestryEntry>();

    /// <summary>
    /// The grants and the permissions their roles allow, read from the library's schema.
    /// </summary>
    public DbSet<EffectiveGrant> Grants => Set<EffectiveGrant>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<HostDocument>(document =>
        {
            document.ToTable("documents", "host");
            document.HasKey(row => row.Id);
            document.Property(row => row.Id).HasColumnName("id");
            document.Property(row => row.Title).HasColumnName("title");
        });

        modelBuilder.MapJanusAuthorization();
    }
}
