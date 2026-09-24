using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sending;

/// <summary>
/// How credit granted to a restriction key is stored.
/// </summary>
/// <remarks>Implements AUTH-ABUSE-004.</remarks>
internal sealed class SendGrantConfiguration : IEntityTypeConfiguration<SendGrantRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SendGrantRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("send_grants", table => table.HasCheckConstraint(
            "ck_send_grants_credit",
            "credit > 0"));

        builder.HasKey(grant => grant.Key).HasName("pk_send_grants");

        builder.Property(grant => grant.Key)
            .HasColumnName("key")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(grant => grant.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(grant => grant.Credit).HasColumnName("credit");
    }
}
