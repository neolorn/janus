using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Callbacks;

/// <summary>
/// How an issued correlation reference is kept.
/// </summary>
/// <remarks>Implements BFF-MACH-003 and INT-GEN-003.</remarks>
internal sealed class CallbackReferenceConfiguration : IEntityTypeConfiguration<CallbackReferenceRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CallbackReferenceRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("callback_references");

        builder.HasKey(issued => issued.Reference).HasName("pk_callback_references");

        builder.Property(issued => issued.Reference)
            .HasColumnName("reference")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(issued => issued.Callback).HasColumnName("callback");
        builder.Property(issued => issued.IssuedAt).HasColumnName("issued_at");
    }
}
