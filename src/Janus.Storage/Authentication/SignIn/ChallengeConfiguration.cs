using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.SignIn;

/// <summary>
/// How a sign-in in progress is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-016 and CONV-ENUM-001. The handle is the key, so the caller's
/// secret resolves to one row and the row is never named anywhere the caller can see.
/// </remarks>
internal sealed class ChallengeConfiguration : IEntityTypeConfiguration<ChallengeRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "signin_challenges";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ChallengeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_signin_challenges_handle",
                $"octet_length(handle) = {Fingerprint.Length}");
        });

        builder.HasKey(challenge => challenge.Handle).HasName("pk_signin_challenges");

        builder.Property(challenge => challenge.Handle)
            .HasColumnName("handle")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(challenge => challenge.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(challenge => challenge.Email)
            .HasColumnName("email")
            .HasConversion(email => email!.Value.Value, value => new IdentifierId(value));

        builder.Property(challenge => challenge.WebAuthn).HasColumnName("webauthn");
        builder.Property(challenge => challenge.CreatedAt).HasColumnName("created_at");
        builder.Property(challenge => challenge.ExpiresAt).HasColumnName("expires_at");
        builder.Property(challenge => challenge.Presented).HasColumnName("presented");

        builder.HasIndex(challenge => challenge.ExpiresAt)
            .HasDatabaseName("ix_signin_challenges_expires_at");

        builder.HasIndex(challenge => challenge.Subject)
            .HasDatabaseName("ix_signin_challenges_subject");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(challenge => challenge.Subject)
            .HasConstraintName("fk_signin_challenges_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
