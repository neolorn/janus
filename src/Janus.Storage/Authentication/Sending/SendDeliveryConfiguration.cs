using System;
using Janus.Authentication.Sending;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How an undelivered message is stored.
/// </summary>
/// <remarks>
/// Implements D-022 and PRIV-RIGHT-005a. No foreign key names the subject: a message
/// may be undertaken for a registration that has no account yet, and the row outlives
/// nothing.
/// </remarks>
internal sealed class SendDeliveryConfiguration : IEntityTypeConfiguration<SendDeliveryRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "send_outbox";

    /// <summary>The column the whole of the message is held in.</summary>
    public const string MessageColumn = "enc_message";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SendDeliveryRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable(Table);

        builder.HasKey(delivery => delivery.Id).HasName("pk_send_outbox");

        builder.Property(delivery => delivery.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new SendDeliveryId(value));

        builder.Property(delivery => delivery.RecordedAt).HasColumnName("recorded_at");

        builder.Property(delivery => delivery.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(delivery => delivery.KeyVersion).HasColumnName("key_version");
        builder.Property(delivery => delivery.WrappedKey).HasColumnName("wrapped_key");
        builder.Property(delivery => delivery.Message).HasColumnName(MessageColumn);

        // D-022: the publisher takes the oldest undelivered messages first.
        builder.HasIndex(delivery => delivery.RecordedAt)
            .HasDatabaseName("ix_send_outbox_recorded_at");
    }
}
