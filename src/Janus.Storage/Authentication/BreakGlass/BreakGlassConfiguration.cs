using System;
using Janus.Authentication.BreakGlass;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.BreakGlass;

/// <summary>
/// How the issues of the break-glass credential and the attempts at it are stored.
/// </summary>
/// <remarks>
/// Implements OPS-BOOT-002 and OPS-BOOT-004. At most one issue stands: the migration
/// adds the unique index over a constant that holds it, which the model has no way to
/// state, and the store serializes the operations that read what stands.
/// </remarks>
internal sealed class BreakGlassConfiguration
    : IEntityTypeConfiguration<BreakGlassCredentialRecord>, IEntityTypeConfiguration<BreakGlassAttemptRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BreakGlassCredentialRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("break_glass_credentials", table =>
        {
            // OPS-BOOT-004: an issue is spent or replaced, never both.
            table.HasCheckConstraint(
                "ck_break_glass_credentials_ended",
                "consumed_at IS NULL OR replaced_at IS NULL");
        });

        builder.HasKey(credential => credential.Id).HasName("pk_break_glass_credentials");

        builder.Property(credential => credential.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new BreakGlassCredentialId(value));

        builder.Property(credential => credential.Hash).HasColumnName("hash");

        builder.Property(credential => credential.IssuedBy)
            .HasColumnName("issued_by")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(credential => credential.IssuedAt).HasColumnName("issued_at");
        builder.Property(credential => credential.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(credential => credential.ReplacedAt).HasColumnName("replaced_at");

        builder.HasIndex(credential => credential.IssuedBy)
            .HasDatabaseName("ix_break_glass_credentials_issued_by");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(credential => credential.IssuedBy)
            .HasConstraintName("fk_break_glass_credentials_issued_by")
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BreakGlassAttemptRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("break_glass_attempts");

        builder.HasKey(attempt => attempt.Id).HasName("pk_break_glass_attempts");

        builder.Property(attempt => attempt.Id).HasColumnName("id");
        builder.Property(attempt => attempt.AttemptedAt).HasColumnName("attempted_at");

        builder.HasIndex(attempt => attempt.AttemptedAt).HasDatabaseName("ix_break_glass_attempts_attempted_at");
    }
}
