using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How a message a transport took is stored until its delivery report can no longer
/// change anything.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004 and INT-SMS-005.</remarks>
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
        builder.Property(send => send.Spent).HasColumnName("spent");
        builder.Property(send => send.SpentVersions).HasColumnName("spent_versions");
        builder.Property(send => send.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(send => send.SentAt).HasColumnName("sent_at");
        builder.Property(send => send.SettlesAt).HasColumnName("settles_at");

        builder.Property(send => send.Send)
            .HasColumnName("send")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(send => send.Admitted).HasColumnName("admitted");
        builder.Property(send => send.Released).HasColumnName("released");
        builder.Property(send => send.Resend).HasColumnName("resend");
        builder.Property(send => send.ResendAt).HasColumnName("resend_at");
        builder.Property(send => send.SecondFactor).HasColumnName("second_factor");

        builder.Property(send => send.Answer)
            .HasColumnName("answer")
            .HasMaxLength(Fingerprint.Length);

        // The settled are swept, so the table holds only what a report could change.
        builder.HasIndex(send => send.SettlesAt).HasDatabaseName("ix_sends_settles_at");

        // The messages of one send are read together, and the sends a code or a link
        // answers are found by what names it.
        builder.HasIndex(send => send.Send).HasDatabaseName("ix_sends_send");
        builder.HasIndex(send => send.Answer).HasDatabaseName("ix_sends_answer");
    }
}
