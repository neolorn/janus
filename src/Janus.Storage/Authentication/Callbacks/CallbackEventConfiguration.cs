using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// How a claimed provider event is kept.
/// </summary>
/// <remarks>
/// Implements BFF-MACH-002. The key is the claim: a second delivery of the event cannot
/// insert its row, whichever of two concurrent deliveries arrives first.
/// </remarks>
internal sealed class CallbackEventConfiguration : IEntityTypeConfiguration<CallbackEventRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CallbackEventRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("callback_events");

        builder.HasKey(claimed => new { claimed.Callback, claimed.Identifier })
            .HasName("pk_callback_events");

        builder.Property(claimed => claimed.Callback).HasColumnName("callback");

        builder.Property(claimed => claimed.Identifier)
            .HasColumnName("identifier")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(claimed => claimed.ClaimedAt).HasColumnName("claimed_at");
    }
}
