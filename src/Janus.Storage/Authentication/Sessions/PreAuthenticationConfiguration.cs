using System;
using Janus.Core;
using Janus.Storage.Authentication.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Sessions;

/// <summary>
/// How a browser's first contact is stored.
/// </summary>
/// <remarks>
/// Implements BFF-CSRF-005a and BFF-CSRF-005b. A browser has at most one registration
/// in flight and a registration belongs to one browser, which the database holds
/// rather than a read before a write; abandoning the registration takes the binding
/// with it and leaves the browser its token.
/// </remarks>
internal sealed class PreAuthenticationConfiguration
    : IEntityTypeConfiguration<PreAuthenticationRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PreAuthenticationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("preauthentication_sessions", table =>
        {
            table.HasCheckConstraint(
                "ck_preauthentication_sessions_fingerprint",
                $"octet_length(fingerprint) = {Fingerprint.Length} AND "
                    + $"octet_length(csrf_fingerprint) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_preauthentication_sessions_expires_at",
                "expires_at > created_at");
        });

        builder.HasKey(contact => contact.Fingerprint).HasName("pk_preauthentication_sessions");

        builder.Property(contact => contact.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(contact => contact.CsrfFingerprint)
            .HasColumnName("csrf_fingerprint")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(contact => contact.CreatedAt).HasColumnName("created_at");
        builder.Property(contact => contact.ExpiresAt).HasColumnName("expires_at");

        builder.Property(contact => contact.Registration)
            .HasColumnName("registration")
            .HasConversion(id => id!.Value.Value, value => new RegistrationSessionId(value));

        builder.HasIndex(contact => contact.ExpiresAt)
            .HasDatabaseName("ix_preauthentication_sessions_expires_at");

        builder.HasIndex(contact => contact.Registration)
            .HasDatabaseName("ux_preauthentication_sessions_registration")
            .IsUnique()
            .HasFilter("registration IS NOT NULL");

        builder.HasOne<RegistrationSessionRecord>()
            .WithMany()
            .HasForeignKey(contact => contact.Registration)
            .HasConstraintName("fk_preauthentication_sessions_registration")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
