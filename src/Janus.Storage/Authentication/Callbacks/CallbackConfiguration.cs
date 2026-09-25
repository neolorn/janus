using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// How an inbound callback is counted.
/// </summary>
/// <remarks>Implements INT-GEN-003.</remarks>
internal sealed class CallbackConfiguration : IEntityTypeConfiguration<CallbackRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CallbackRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("callbacks");

        builder.HasKey(callback => callback.Id).HasName("pk_callbacks");

        builder.Property(callback => callback.Id).HasColumnName("id");

        builder.Property(callback => callback.Source)
            .HasColumnName("source")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(callback => callback.At).HasColumnName("at");
        builder.Property(callback => callback.Rejected).HasColumnName("rejected");

        // The limit and the alert are both questions about one source over a span.
        builder.HasIndex(callback => new { callback.Source, callback.At })
            .HasDatabaseName("ix_callbacks_source_at");
    }
}
