using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Background;

/// <summary>
/// How a background job's runs are kept.
/// </summary>
/// <remarks>Implements INF-BG-001.</remarks>
internal sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJobRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BackgroundJobRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("background_jobs");

        builder.HasKey(job => job.Name).HasName("pk_background_jobs");

        builder.Property(job => job.Name).HasColumnName("name");
        builder.Property(job => job.RecordedAt).HasColumnName("recorded_at");
        builder.Property(job => job.AttemptedAt).HasColumnName("attempted_at");
        builder.Property(job => job.SucceededAt).HasColumnName("succeeded_at");
        builder.Property(job => job.LapseRaisedAt).HasColumnName("lapse_raised_at");
    }
}
