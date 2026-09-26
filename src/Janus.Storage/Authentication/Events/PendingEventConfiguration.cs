using System;
using Janus.Authentication.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Events;

/// <summary>
/// How an emitted event waits for its consumers.
/// </summary>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002 and D-162 item 29. A row every consumer has
/// taken is marked rather than removed. The due index holds only the rows still
/// waiting, so a pass reads them without passing over the marked ones.
/// </remarks>
internal sealed class PendingEventConfiguration : IEntityTypeConfiguration<PendingEventRecord>
{
    private const int KindLength = 64;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PendingEventRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("events");

        builder.HasKey(pending => pending.Id).HasName("pk_events");

        builder.Property(pending => pending.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new PendingEventId(value));

        builder.Property(pending => pending.Kind)
            .HasColumnName("kind")
            .HasMaxLength(KindLength);

        builder.Property(pending => pending.RaisedAt).HasColumnName("raised_at");

        builder.Property(pending => pending.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb");

        builder.Property(pending => pending.Attempts).HasColumnName("attempts");

        builder.Property(pending => pending.NextAttemptAt).HasColumnName("next_attempt_at");

        builder.Property(pending => pending.TakenBy)
            .HasColumnName("taken_by")
            .HasColumnType("jsonb");

        builder.Property(pending => pending.PublishedAt).HasColumnName("published_at");

        builder.Property(pending => pending.FailedAt).HasColumnName("failed_at");

        builder.HasIndex(pending => pending.NextAttemptAt)
            .HasDatabaseName("ix_events_due")
            .HasFilter("published_at IS NULL AND failed_at IS NULL");
    }
}
