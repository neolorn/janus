using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How a non-existence notice is stored.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-003.</remarks>
internal sealed class NoticeConfiguration : IEntityTypeConfiguration<NoticeRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<NoticeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("nonexistence_notices");

        builder.HasKey(notice => notice.Id).HasName("pk_nonexistence_notices");

        builder.Property(notice => notice.Id).HasColumnName("id");

        builder.Property(notice => notice.Destination)
            .HasColumnName("destination")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(notice => notice.At).HasColumnName("at");

        // One notice per address per window is a question about one address.
        builder.HasIndex(notice => new { notice.Destination, notice.At })
            .HasDatabaseName("ix_nonexistence_notices_destination_at");
    }
}
