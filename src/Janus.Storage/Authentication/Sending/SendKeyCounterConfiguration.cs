using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How what a key other than a destination has been counted is stored.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004 and PRIV-RET-005.</remarks>
internal sealed class SendKeyCounterConfiguration : IEntityTypeConfiguration<SendKeyCounterRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SendKeyCounterRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("send_key_counters");

        builder.HasKey(counter => counter.Key).HasName("pk_send_key_counters");

        builder.Property(counter => counter.Key)
            .HasColumnName("key")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(counter => counter.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(counter => counter.SentAt).HasColumnName("sent_at");

        // The sweep reads the newest of the times, which no model builder expresses as
        // an index; the migration writes it over the same expression the delete uses.
    }
}
