using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// How a browser an account knows is stored.
/// </summary>
/// <remarks>Implements AUTH-FACT-015, AUTH-FACT-016 and CONV-ENUM-001.</remarks>
internal sealed class DeviceConfiguration : IEntityTypeConfiguration<DeviceRecord>
{
    private const int LabelLength = 64;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DeviceRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("devices", table => table.HasCheckConstraint(
            "ck_devices_kind",
            Vocabulary.Admits<DeviceKind>("kind")));

        builder.HasKey(device => device.Id).HasName("pk_devices");

        builder.Property(device => device.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new DeviceId(value));

        builder.Property(device => device.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(device => device.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<DeviceKind>());

        builder.Property(device => device.Label)
            .HasColumnName("label")
            .HasMaxLength(LabelLength);

        builder.Property(device => device.TokenFingerprint)
            .HasColumnName("token_fingerprint")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(device => device.CreatedAt).HasColumnName("created_at");
        builder.Property(device => device.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(device => device.ExpiresAt).HasColumnName("expires_at");
        builder.Property(device => device.ConsecutiveFailures).HasColumnName("consecutive_failures");
        builder.Property(device => device.Revoked).HasColumnName("revoked");

        // AUTH-FACT-015: the cookie is looked up by what it fingerprints to.
        builder.HasIndex(device => device.TokenFingerprint)
            .HasDatabaseName("ux_devices_token_fingerprint")
            .IsUnique();

        // AUTH-FACT-015 AC5: the account lists the browsers it knows.
        builder.HasIndex(device => device.Subject).HasDatabaseName("ix_devices_subject");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(device => device.Subject)
            .HasConstraintName("fk_devices_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
