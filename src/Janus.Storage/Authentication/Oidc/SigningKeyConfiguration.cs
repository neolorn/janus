using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How a token signing key is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The private material is wrapped under the
/// deployment's data key, so a dump of this table signs nothing; the public material is
/// what the key set publishes and is not a secret. The table admits one next key and one
/// current key, so of two processes making the same key or change one makes it.
/// </remarks>
internal sealed class SigningKeyConfiguration : IEntityTypeConfiguration<SigningKeyRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "signing_keys";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SigningKeyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            // AUTH-KEY-001: a key is next, signing nothing and carrying no lifetime;
            // current, signing since it was published or later; or replaced, its
            // overlap ending after its replacement and its keeping no sooner.
            table.HasCheckConstraint(
                "ck_signing_keys_stage",
                "(signing_from IS NULL AND longest_lifetime = interval '0'"
                    + " AND superseded_at IS NULL AND retires_at IS NULL AND kept_until IS NULL)"
                    + " OR (signing_from >= created_at"
                    + " AND superseded_at IS NULL AND retires_at IS NULL AND kept_until IS NULL)"
                    + " OR (signing_from >= created_at AND superseded_at >= signing_from"
                    + " AND retires_at > superseded_at AND kept_until >= retires_at)");

            // AUTH-KEY-001 AC5: only a replaced key goes without its private key.
            table.HasCheckConstraint(
                "ck_signing_keys_private_key",
                "private_key IS NOT NULL OR retires_at IS NOT NULL");
        });

        builder.HasKey(key => key.KeyId).HasName("pk_signing_keys");

        builder.Property(key => key.KeyId).HasColumnName("key_id");
        builder.Property(key => key.Algorithm).HasColumnName("algorithm");
        builder.Property(key => key.PublicKey).HasColumnName("public_key");
        builder.Property(key => key.PrivateKey).HasColumnName("private_key");
        builder.Property(key => key.CreatedAt).HasColumnName("created_at");
        builder.Property(key => key.SigningFrom).HasColumnName("signing_from");
        builder.Property(key => key.LongestLifetime).HasColumnName("longest_lifetime");
        builder.Property(key => key.SupersededAt).HasColumnName("superseded_at");
        builder.Property(key => key.RetiresAt).HasColumnName("retires_at");
        builder.Property(key => key.KeptUntil).HasColumnName("kept_until");

        builder.Property(key => key.IsNext)
            .HasColumnName("is_next")
            .HasComputedColumnSql("signing_from IS NULL", stored: true);

        builder.Property(key => key.IsCurrent)
            .HasColumnName("is_current")
            .HasComputedColumnSql("signing_from IS NOT NULL AND superseded_at IS NULL", stored: true);

        builder.HasIndex(key => key.RetiresAt).HasDatabaseName("ix_signing_keys_retires_at");

        // AUTH-KEY-001 AC7, D-166 X3: one next key and one current key.
        builder.HasIndex(key => key.IsNext)
            .HasDatabaseName("ux_signing_keys_next")
            .IsUnique()
            .HasFilter("is_next");

        builder.HasIndex(key => key.IsCurrent)
            .HasDatabaseName("ux_signing_keys_current")
            .IsUnique()
            .HasFilter("is_current");
    }
}
