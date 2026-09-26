using System;
using Janus.Authentication.Credentials;
using Janus.Core;
using Janus.Storage.Authentication.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// How a round trip to a social provider is stored.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-012, REG-IDENT-008, BFF-CSRF-005a and OPS-SEC-001. A browser has
/// at most one in flight, bound to exactly one of its pre-authentication session and
/// its session, which the database holds rather than a read before a write; the
/// attempt goes with what it is bound to. The proof key and the version it is wrapped
/// under move together.
/// </remarks>
internal sealed class ProviderAttemptConfiguration : IEntityTypeConfiguration<ProviderAttemptRecord>
{
    /// <summary>The table, which the re-wrap of the key-encryption key names.</summary>
    public const string Table = "provider_attempts";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProviderAttemptRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_provider_attempts_binding",
                "num_nonnulls(preauthentication, session) = 1");
            table.HasCheckConstraint(
                "ck_provider_attempts_provider",
                Vocabulary.Admits<Factor>("provider"));
            table.HasCheckConstraint(
                "ck_provider_attempts_intent",
                Vocabulary.Admits<ProviderIntent>("intent"));
            table.HasCheckConstraint(
                "ck_provider_attempts_fingerprints",
                $"octet_length(state) = {Fingerprint.Length} AND "
                    + $"octet_length(nonce) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_provider_attempts_verifier",
                "num_nulls(verifier, key_version) IN (0, 2)");
        });

        builder.HasKey(attempt => attempt.Id).HasName("pk_provider_attempts");

        builder.Property(attempt => attempt.Id).HasColumnName("id");

        builder.Property(attempt => attempt.PreAuthentication)
            .HasColumnName("preauthentication")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(attempt => attempt.Session)
            .HasColumnName("session")
            .HasConversion(id => id!.Value.Value, value => new SessionId(value));

        builder.Property(attempt => attempt.Provider)
            .HasColumnName("provider")
            .HasConversion(new VocabularyConverter<Factor>());

        builder.Property(attempt => attempt.Intent)
            .HasColumnName("intent")
            .HasConversion(new VocabularyConverter<ProviderIntent>());

        builder.Property(attempt => attempt.State)
            .HasColumnName("state")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(attempt => attempt.Nonce)
            .HasColumnName("nonce")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(attempt => attempt.Verifier).HasColumnName("verifier");
        builder.Property(attempt => attempt.KeyVersion).HasColumnName("key_version");
        builder.Property(attempt => attempt.ReturnTo).HasColumnName("return_to");
        builder.Property(attempt => attempt.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(attempt => attempt.PreAuthentication)
            .HasDatabaseName("ux_provider_attempts_preauthentication")
            .IsUnique()
            .HasFilter("preauthentication IS NOT NULL");

        builder.HasIndex(attempt => attempt.Session)
            .HasDatabaseName("ux_provider_attempts_session")
            .IsUnique()
            .HasFilter("session IS NOT NULL");

        builder.HasOne<PreAuthenticationRecord>()
            .WithMany()
            .HasForeignKey(attempt => attempt.PreAuthentication)
            .HasConstraintName("fk_provider_attempts_preauthentication")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SessionRecord>()
            .WithMany()
            .HasForeignKey(attempt => attempt.Session)
            .HasConstraintName("fk_provider_attempts_session")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
