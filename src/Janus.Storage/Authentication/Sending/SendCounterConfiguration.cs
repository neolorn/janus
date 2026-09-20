using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How what a restriction key has been sent is stored.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004.</remarks>
internal sealed class SendCounterConfiguration : IEntityTypeConfiguration<SendCounterRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SendCounterRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("send_counters");

        builder.HasKey(counter => counter.Key).HasName("pk_send_counters");

        builder.Property(counter => counter.Key)
            .HasColumnName("key")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(counter => counter.SentAt).HasColumnName("sent_at");
        builder.Property(counter => counter.SettlesAt).HasColumnName("settles_at");

        // The aged out are swept, so a key sent to once does not stand for ever.
        builder.HasIndex(counter => counter.SettlesAt).HasDatabaseName("ix_send_counters_settles_at");
    }
}
