using System;
using Janus.Privacy.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Outbox;

/// <summary>
/// How one subscriber's confirmation is stored.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a and CONV-DESIGN-003. The key is the delivery and the
/// subscriber together, so the same subscriber confirming the same delivery twice is
/// the one row it was.
/// </remarks>
internal sealed class DeliveryConfirmationConfiguration
    : IEntityTypeConfiguration<DeliveryConfirmationRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DeliveryConfirmationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_confirmations", table => table.HasCheckConstraint(
            "ck_outbox_confirmations_subscriber",
            "length(trim(subscriber)) > 0"));

        builder.HasKey(confirmation => new { confirmation.Delivery, confirmation.Subscriber })
            .HasName("pk_outbox_confirmations");

        builder.Property(confirmation => confirmation.Delivery)
            .HasColumnName("delivery")
            .HasConversion(id => id.Value, value => new DeliveryId(value));

        builder.Property(confirmation => confirmation.Subscriber).HasColumnName("subscriber");

        builder.Property(confirmation => confirmation.ConfirmedAt).HasColumnName("confirmed_at");
    }
}
