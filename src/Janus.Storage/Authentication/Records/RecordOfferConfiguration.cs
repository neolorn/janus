using System;
using Janus.Core;
using Janus.Storage.Authentication.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// How the offer to attach an account session holds is stored.
/// </summary>
/// <remarks>
/// Implements REC-ATT-001. The offer is held on the session, so it goes with it; the
/// records counted are named by subject with no foreign key, since the attachment judges
/// each again under its lock.
/// </remarks>
internal sealed class RecordOfferConfiguration : IEntityTypeConfiguration<RecordOfferRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecordOfferRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("record_offers");

        builder.HasKey(offer => offer.SessionId).HasName("pk_record_offers");

        builder.Property(offer => offer.SessionId)
            .HasColumnName("session_id")
            .HasConversion(id => id.Value, value => new SessionId(value));

        builder.Property(offer => offer.Proven)
            .HasColumnName("proven")
            .HasColumnType("jsonb");

        builder.Property(offer => offer.Offered).HasColumnName("offered");
        builder.Property(offer => offer.ExpiresAt).HasColumnName("expires_at");

        builder.HasOne<SessionRecord>()
            .WithOne()
            .HasForeignKey<RecordOfferRecord>(offer => offer.SessionId)
            .HasConstraintName("fk_record_offers_session_id")
            .OnDelete(DeleteBehavior.Cascade);

        // REC-ATT-001: the sweep reads the offers that have lapsed and no others.
        builder.HasIndex(offer => offer.ExpiresAt)
            .HasDatabaseName("ix_record_offers_expires_at");
    }
}
