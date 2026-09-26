using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Gate;

/// <summary>
/// How each person's daily mean is kept.
/// </summary>
/// <remarks>Implements OPS-ALERT-005.</remarks>
internal sealed class ReadBaselineConfiguration : IEntityTypeConfiguration<ReadBaselineRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ReadBaselineRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("read_baselines");

        builder.HasKey(baseline => baseline.Actor).HasName("pk_read_baselines");

        builder.Property(baseline => baseline.Actor)
            .HasColumnName("actor")
            .HasConversion(actor => actor.Value, value => new SubjectId(value));

        builder.Property(baseline => baseline.DailyMean).HasColumnName("daily_mean");
    }
}
