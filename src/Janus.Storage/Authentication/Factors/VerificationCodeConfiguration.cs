using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// How a verification code is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-004 AC2. The codes have a table of their own with an expiry of
/// their own, so nothing about a code can be reached through what a credential is
/// held in.
/// </remarks>
internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCodeRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "verification_codes";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<VerificationCodeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_verification_codes_holder",
                "octet_length(holder) > 0");

            table.HasCheckConstraint(
                "ck_verification_codes_attempts",
                "attempts >= 0");
        });

        builder.HasKey(code => code.Holder).HasName("pk_verification_codes");

        builder.Property(code => code.Holder).HasColumnName("holder");
        builder.Property(code => code.Code).HasColumnName("code");
        builder.Property(code => code.IssuedAt).HasColumnName("issued_at");
        builder.Property(code => code.ExpiresAt).HasColumnName("expires_at");
        builder.Property(code => code.Attempts).HasColumnName("attempts");

        builder.HasIndex(code => code.ExpiresAt)
            .HasDatabaseName("ix_verification_codes_expires_at");
    }
}
