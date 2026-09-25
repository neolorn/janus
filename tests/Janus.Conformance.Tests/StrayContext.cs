using System;
using Microsoft.EntityFrameworkCore;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's context with one more entity mapped, which its declaration names
/// no policy for.
/// </summary>
/// <param name="options">How the context reaches the database.</param>
internal sealed class StrayContext(DbContextOptions options) : SampleContext(options)
{
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Label>(label =>
        {
            label.ToTable("labels", "sample");
            label.HasKey(row => row.Id);
            label.Property(row => row.Id).HasColumnName("id");
        });
    }
}
