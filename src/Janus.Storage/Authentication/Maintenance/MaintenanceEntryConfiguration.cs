using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Maintenance;

/// <summary>
/// How the maintenance log is stored.
/// </summary>
/// <remarks>
/// Implements OPS-MAINT-001. No foreign key names the actor: the log outlives the
/// account of whoever performed a task, and the entry keeps the identifier alone.
/// </remarks>
internal sealed class MaintenanceEntryConfiguration : IEntityTypeConfiguration<MaintenanceEntryRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MaintenanceEntryRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("maintenance_log");

        builder.HasKey(entry => entry.Id).HasName("pk_maintenance_log");

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new MaintenanceEntryId(value));

        builder.Property(entry => entry.Task)
            .HasColumnName("task")
            .HasConversion(new VocabularyConverter<MaintenanceTask>());

        builder.Property(entry => entry.PerformedAt).HasColumnName("performed_at");

        builder.Property(entry => entry.Actor)
            .HasColumnName("actor")
            .HasConversion(actor => actor.Value, value => new SubjectId(value));

        builder.Property(entry => entry.Note).HasColumnName("note");
    }
}
