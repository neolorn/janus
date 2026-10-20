using System;
using Janus.Core;
using Janus.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HostSample;

/// <summary>
/// The workshop's own context: its tables, and the library's tables a permission filter
/// reads, mapped beside them (LIB-HOST-002).
/// </summary>
/// <param name="options">Where the context reaches the deployment's database.</param>
internal sealed class WorkshopContext(DbContextOptions<WorkshopContext> options) : DbContext(options)
{
    // A person is held by the identifier the library knows them by.
    private static readonly ValueConverter<SubjectId, Guid> Subject = new(
        subject => subject.Value,
        value => new SubjectId(value));

    /// <summary>
    /// The workshops.
    /// </summary>
    public DbSet<Workshop> Workshops => Set<Workshop>();

    /// <summary>
    /// The kites taken in for repair.
    /// </summary>
    public DbSet<Repair> Repairs => Set<Repair>();

    /// <summary>
    /// Whose kite each repair is.
    /// </summary>
    public DbSet<RepairOwner> Owners => Set<RepairOwner>();

    /// <summary>
    /// Who each owner named to collect their kite.
    /// </summary>
    public DbSet<RepairCollector> Collectors => Set<RepairCollector>();

    /// <summary>
    /// The grip fittings made for customers.
    /// </summary>
    public DbSet<Fitting> Fittings => Set<Fitting>();

    /// <summary>
    /// The ancestry closure, read from the library's schema.
    /// </summary>
    public DbSet<AncestryEntry> Ancestry => Set<AncestryEntry>();

    /// <summary>
    /// The grants and the permissions their roles allow, read from the library's schema.
    /// </summary>
    public DbSet<EffectiveGrant> Grants => Set<EffectiveGrant>();

    /// <summary>
    /// The records whose data subject holds a live consent, read from the library's schema.
    /// </summary>
    public DbSet<ConsentedResource> Consented => Set<ConsentedResource>();

    /// <summary>
    /// What the permission filter of a repair reads: the library's tables, and the facts
    /// of the workshop's own data the repair's derivations follow from.
    /// </summary>
    /// <returns>The sources.</returns>
    public FilterSources<Repair> RepairSources() =>
        new FilterSources<Repair>(Ancestry, Grants, Consented, repair => repair.Id)
            .Relationship(WorkshopModel.Owner, Owners)
            .Relationship(WorkshopModel.Collector, Collectors);

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Workshop>(workshop =>
        {
            workshop.ToTable("workshops", WorkshopSchema.Name);
            workshop.HasKey(row => row.Id);
            workshop.Property(row => row.Id).HasColumnName("id");
            workshop.Property(row => row.Organization).HasColumnName("organization");
            workshop.Property(row => row.Name).HasColumnName("name");
            workshop.Property(row => row.Capacity).HasColumnName("capacity");
        });

        modelBuilder.Entity<Repair>(repair =>
        {
            repair.ToTable("repairs", WorkshopSchema.Name);
            repair.HasKey(row => row.Id);
            repair.Property(row => row.Id).HasColumnName("id");
            repair.Property(row => row.WorkshopId).HasColumnName("workshop_id");
            repair.Property(row => row.Customer).HasColumnName("customer").HasConversion(Subject);
            repair.Property(row => row.Kite).HasColumnName("kite");
            repair.Property(row => row.ContactName).HasColumnName("contact_name");
            repair.Property(row => row.FollowUp).HasColumnName("follow_up");
            repair.Property(row => row.ReleasedAt).HasColumnName("released_at");
        });

        modelBuilder.Entity<RepairOwner>(owner =>
        {
            owner.ToTable("owners", WorkshopSchema.Name);
            owner.HasKey(row => new { row.RepairId, row.Owner });
            owner.Property(row => row.RepairId).HasColumnName("repair_id");
            owner.Property(row => row.Owner).HasColumnName("owner").HasConversion(Subject);
        });

        modelBuilder.Entity<RepairCollector>(collector =>
        {
            collector.ToTable("collectors", WorkshopSchema.Name);
            collector.HasKey(row => new { row.RepairId, row.Collector });
            collector.Property(row => row.RepairId).HasColumnName("repair_id");
            collector.Property(row => row.Collector).HasColumnName("collector").HasConversion(Subject);
        });

        modelBuilder.Entity<Fitting>(fitting =>
        {
            fitting.ToTable("fittings", WorkshopSchema.Name);
            fitting.HasKey(row => row.Id);
            fitting.Property(row => row.Id).HasColumnName("id");
            fitting.Property(row => row.RepairId).HasColumnName("repair_id");
            fitting.Property(row => row.Customer).HasColumnName("customer").HasConversion(Subject);
            fitting.Property(row => row.Need).HasColumnName("need");
        });

        modelBuilder.MapAuthorizationTables();
    }
}
