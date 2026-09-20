using System;
using Janus.Core;
using Janus.Storage.Authentication.Factors;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// How the creation ceremony an account has open is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-014, AUTH-STEP-007 and CONV-ENUM-001. The account is the key,
/// so opening a ceremony replaces whatever stood before it and an abandoned challenge
/// is never a second way in.
/// </remarks>
internal sealed class KeyCeremonyConfiguration : IEntityTypeConfiguration<KeyCeremonyRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "key_ceremonies";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<KeyCeremonyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_key_ceremonies_kind",
                Vocabulary.Admits<Factor>("kind"));
            table.HasCheckConstraint(
                "ck_key_ceremonies_expiry",
                "expires_at > issued_at");
        });

        builder.HasKey(ceremony => ceremony.Subject).HasName("pk_key_ceremonies");

        builder.Property(ceremony => ceremony.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(ceremony => ceremony.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<Factor>());

        builder.Property(ceremony => ceremony.Challenge).HasColumnName("challenge");

        builder.Property(ceremony => ceremony.Upgrading)
            .HasColumnName("upgrading")
            .HasConversion(upgrading => upgrading!.Value.Value, value => new AuthenticatorId(value));

        builder.Property(ceremony => ceremony.IssuedAt).HasColumnName("issued_at");
        builder.Property(ceremony => ceremony.ExpiresAt).HasColumnName("expires_at");

        builder.HasIndex(ceremony => ceremony.ExpiresAt)
            .HasDatabaseName("ix_key_ceremonies_expires_at");

        builder.HasIndex(ceremony => ceremony.Upgrading)
            .HasDatabaseName("ix_key_ceremonies_upgrading");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(ceremony => ceremony.Subject)
            .HasConstraintName("fk_key_ceremonies_subject")
            .OnDelete(DeleteBehavior.Cascade);

        // The entry an upgrade retires goes with it: a ceremony that would replace a
        // credential the account no longer holds has nothing left to do.
        builder.HasOne<AuthenticatorRecord>()
            .WithMany()
            .HasForeignKey(ceremony => ceremony.Upgrading)
            .HasConstraintName("fk_key_ceremonies_upgrading")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
