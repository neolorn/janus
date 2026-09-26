using System;
using Janus.Core;
using Janus.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Janus.Conformance.Tests;

/// <summary>
/// The sample host's own context: its three kinds of thing, the two facts it derives
/// roles from, and the library's two contract tables mapped beside them.
/// </summary>
/// <param name="options">How the context reaches the database.</param>
internal class SampleContext(DbContextOptions options) : DbContext(options)
{
    /// <summary>
    /// The shelves.
    /// </summary>
    public DbSet<Shelf> Shelves => Set<Shelf>();

    /// <summary>
    /// The binders.
    /// </summary>
    public DbSet<Binder> Binders => Set<Binder>();

    /// <summary>
    /// The sheets.
    /// </summary>
    public DbSet<Sheet> Sheets => Set<Sheet>();

    /// <summary>
    /// Who keeps which shelf.
    /// </summary>
    public DbSet<ShelfKeeper> Keepers => Set<ShelfKeeper>();

    /// <summary>
    /// Who looks after which binder.
    /// </summary>
    public DbSet<BinderSteward> Stewards => Set<BinderSteward>();

    /// <summary>
    /// The ancestry the library keeps.
    /// </summary>
    public DbSet<AncestryEntry> Ancestry => Set<AncestryEntry>();

    /// <summary>
    /// The grants in effect.
    /// </summary>
    public DbSet<EffectiveGrant> Grants => Set<EffectiveGrant>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Shelf>(shelf =>
        {
            shelf.ToTable("shelves", "sample");
            shelf.HasKey(row => row.Id);
            shelf.Property(row => row.Id).HasColumnName("id");
            shelf.Property(row => row.Organization).HasColumnName("organization");
        });

        modelBuilder.Entity<Binder>(binder =>
        {
            binder.ToTable("binders", "sample");
            binder.HasKey(row => row.Id);
            binder.Property(row => row.Id).HasColumnName("id");
            binder.Property(row => row.ShelfId).HasColumnName("shelf_id");
        });

        modelBuilder.Entity<Sheet>(sheet =>
        {
            sheet.ToTable("sheets", "sample");
            sheet.HasKey(row => row.Id);
            sheet.Property(row => row.Id).HasColumnName("id");
            sheet.Property(row => row.BinderId).HasColumnName("binder_id");
        });

        modelBuilder.Entity<ShelfKeeper>(keeper =>
        {
            keeper.ToTable("keepers", "sample");
            keeper.HasKey(row => new { row.ShelfId, row.Keeper });
            keeper.Property(row => row.ShelfId).HasColumnName("shelf_id");
            keeper.Property(row => row.Keeper)
                .HasColumnName("keeper")
                .HasConversion(subject => subject.Value, value => new SubjectId(value));
        });

        modelBuilder.Entity<BinderSteward>(steward =>
        {
            steward.ToTable("stewards", "sample");
            steward.HasKey(row => new { row.BinderId, row.Steward });
            steward.Property(row => row.BinderId).HasColumnName("binder_id");
            steward.Property(row => row.Steward)
                .HasColumnName("steward")
                .HasConversion(subject => subject.Value, value => new SubjectId(value));
        });

        modelBuilder.MapAuthorizationTables();
    }
}
