using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Registration;

/// <summary>
/// How an outstanding verification link is stored.
/// </summary>
/// <remarks>
/// Implements REG-SESS-003. Deleting the session takes its links with it, so a token
/// of an abandoned registration resolves to nothing.
/// </remarks>
internal sealed class RegistrationLinkConfiguration
    : IEntityTypeConfiguration<RegistrationLinkRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RegistrationLinkRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.ToTable("registration_links");

        builder.HasKey(link => link.Fingerprint).HasName("pk_registration_links");

        builder.Property(link => link.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(link => link.Session)
            .HasColumnName("session")
            .HasConversion(id => id.Value, value => new RegistrationSessionId(value));

        builder.HasIndex(link => link.Session).HasDatabaseName("ix_registration_links_session");

        builder.HasOne<RegistrationSessionRecord>()
            .WithMany()
            .HasForeignKey(link => link.Session)
            .HasConstraintName("fk_registration_links_session")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
