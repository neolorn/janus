using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// How a username held after an erasure is stored.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-009 and CONV-DESIGN-003. The row outlives the account it came
/// from and names no subject: what it keeps is that the name is not free yet.
/// </remarks>
internal sealed class UsernameHoldConfiguration : IEntityTypeConfiguration<UsernameHoldRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UsernameHoldRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("username_holds", table => table.HasCheckConstraint(
            "ck_username_holds_fingerprint",
            string.Create(
                CultureInfo.InvariantCulture,
                $"octet_length(fingerprint) = {Fingerprint.Length}")));

        builder.HasKey(hold => hold.Fingerprint).HasName("pk_username_holds");

        builder.Property(hold => hold.Fingerprint).HasColumnName("fingerprint");
        builder.Property(hold => hold.HeldFrom).HasColumnName("held_from");
        builder.Property(hold => hold.ReleasesAt).HasColumnName("releases_at");

        // The sweep reads the holds that have run out and nothing else.
        builder.HasIndex(hold => hold.ReleasesAt)
            .HasDatabaseName("ix_username_holds_releases_at");
    }
}
