using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// How the records each person was given are counted by day.
/// </summary>
/// <remarks>Implements OPS-ALERT-005.</remarks>
internal sealed class ReadVolumeConfiguration : IEntityTypeConfiguration<ReadVolumeRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReadVolumeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("read_volume");

        builder.HasKey(read => new { read.Actor, read.Day }).HasName("pk_read_volume");

        builder.Property(read => read.Actor)
            .HasColumnName("actor")
            .HasConversion(actor => actor.Value, value => new SubjectId(value));

        builder.Property(read => read.Day).HasColumnName("day");

        builder.Property(read => read.Records).HasColumnName("records");

        // The job forgets by day, across every person.
        builder.HasIndex(read => read.Day).HasDatabaseName("ix_read_volume_day");
    }
}
