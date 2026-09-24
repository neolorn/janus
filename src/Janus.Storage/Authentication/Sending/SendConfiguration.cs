using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How a message a transport took is stored until its delivery report can no longer
/// change anything.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004, AUTH-ABUSE-007 and INT-SMS-005.</remarks>
internal sealed class SendConfiguration : IEntityTypeConfiguration<SendRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SendRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sends");

        builder.HasKey(send => send.Reference).HasName("pk_sends");

        builder.Property(send => send.Reference)
            .HasColumnName("reference")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(send => send.Counted).HasColumnName("counted");
        builder.Property(send => send.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(send => send.SentAt).HasColumnName("sent_at");
        builder.Property(send => send.SettlesAt).HasColumnName("settles_at");

        // The settled are swept, so the table holds only what a report could change.
        builder.HasIndex(send => send.SettlesAt).HasDatabaseName("ix_sends_settles_at");
    }
}
