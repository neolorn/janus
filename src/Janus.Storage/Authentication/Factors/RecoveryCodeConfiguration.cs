using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// How a set of recovery codes is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-008 and AUTH-FACT-009. The codes are children of the set, so
/// replacing the set replaces every code of it in one statement.
/// </remarks>
internal sealed class RecoveryCodeConfiguration
    : IEntityTypeConfiguration<RecoveryCodeSetRecord>, IEntityTypeConfiguration<RecoveryCodeRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecoveryCodeSetRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("recovery_code_sets");

        builder.HasKey(set => set.Subject).HasName("pk_recovery_code_sets");

        builder.Property(set => set.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(set => set.GeneratedAt).HasColumnName("generated_at");
        builder.Property(set => set.ViewedAt).HasColumnName("viewed_at");
        builder.Property(set => set.ExportedAt).HasColumnName("exported_at");
        builder.Property(set => set.RemindedAt).HasColumnName("reminded_at");

        builder.HasOne<AccountRecord>()
            .WithOne()
            .HasForeignKey<RecoveryCodeSetRecord>(set => set.Subject)
            .HasConstraintName("fk_recovery_code_sets_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecoveryCodeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("recovery_codes");

        builder.HasKey(code => new { code.Subject, code.Ordinal }).HasName("pk_recovery_codes");

        builder.Property(code => code.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(code => code.Ordinal).HasColumnName("ordinal");
        builder.Property(code => code.Hash).HasColumnName("hash");
        builder.Property(code => code.UsedAt).HasColumnName("used_at");

        // AUTH-FACT-009: a set is replaced whole, so the codes go with the set they
        // belong to and none of a previous set survives it.
        builder.HasOne<RecoveryCodeSetRecord>()
            .WithMany()
            .HasForeignKey(code => code.Subject)
            .HasConstraintName("fk_recovery_codes_set")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
