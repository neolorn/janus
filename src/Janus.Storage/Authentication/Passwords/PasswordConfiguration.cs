using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Passwords;

/// <summary>
/// How a password is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-PASS-003, AUTH-PASS-006 and AUTH-PASS-007. There is no expiry
/// column and no hint column: neither mechanism exists, so neither has a place to
/// live.
/// </remarks>
internal sealed class PasswordConfiguration : IEntityTypeConfiguration<PasswordRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PasswordRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("passwords");

        builder.HasKey(password => password.Subject).HasName("pk_passwords");

        builder.Property(password => password.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(password => password.Hash).HasColumnName("hash");

        builder.Property(password => password.MeetsSingleFactorFloor)
            .HasColumnName("meets_single_factor_floor");

        builder.Property(password => password.SetAt).HasColumnName("set_at");

        builder.HasOne<AccountRecord>()
            .WithOne()
            .HasForeignKey<PasswordRecord>(password => password.Subject)
            .HasConstraintName("fk_passwords_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
