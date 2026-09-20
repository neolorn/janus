using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How a token signing key is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-KEY-001 and AUTH-KEY-002. The private material is wrapped under the
/// key-encryption key the deployment fetches at startup, so a dump of this table signs
/// nothing; the public material is what the key set publishes and is not a secret.
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
            table.HasCheckConstraint("ck_signing_keys_version", "key_version >= 1");

            // AUTH-KEY-001: a key stops signing and leaves the published set together,
            // so the one that is still signing carries neither moment.
            table.HasCheckConstraint(
                "ck_signing_keys_retirement",
                "(superseded_at IS NULL AND retires_at IS NULL)"
                    + " OR (superseded_at IS NOT NULL AND retires_at > superseded_at)");
        });

        builder.HasKey(key => key.KeyId).HasName("pk_signing_keys");

        builder.Property(key => key.KeyId).HasColumnName("key_id");
        builder.Property(key => key.Algorithm).HasColumnName("algorithm");
        builder.Property(key => key.PublicKey).HasColumnName("public_key");
        builder.Property(key => key.PrivateKey).HasColumnName("private_key");
        builder.Property(key => key.KeyVersion).HasColumnName("key_version");
        builder.Property(key => key.CreatedAt).HasColumnName("created_at");
        builder.Property(key => key.SupersededAt).HasColumnName("superseded_at");
        builder.Property(key => key.RetiresAt).HasColumnName("retires_at");

        builder.HasIndex(key => key.RetiresAt).HasDatabaseName("ix_signing_keys_retires_at");

        // OPS-SEC-003: the re-wrap reads the keys still under the previous version.
        builder.HasIndex(key => key.KeyVersion).HasDatabaseName("ix_signing_keys_key_version");
    }
}
