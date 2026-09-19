using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Profiles;

/// <summary>
/// How an account's profile is stored.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-007, IDN-PRIN-003, PRIV-RIGHT-005a and CONV-DESIGN-003. The row
/// belongs to the account and is never removed with it: erasure destroys the key the
/// three columns are written under and leaves the row where it is.
/// </remarks>
internal sealed class ProfileConfiguration : IEntityTypeConfiguration<ProfileRecord>
{
    /// <summary>
    /// The table the three encrypted columns name as their location.
    /// </summary>
    public const string Table = "profiles";

    /// <summary>
    /// The column the display name is written to.
    /// </summary>
    public const string DisplayNameColumn = "enc_display_name";

    /// <summary>
    /// The column the legal name is written to.
    /// </summary>
    public const string LegalNameColumn = "enc_legal_name";

    /// <summary>
    /// The column the date of birth is written to.
    /// </summary>
    public const string DateOfBirthColumn = "enc_date_of_birth";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProfileRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(profile => profile.Subject).HasName("pk_profiles");

        builder.Property(profile => profile.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(profile => profile.DisplayName).HasColumnName(DisplayNameColumn);
        builder.Property(profile => profile.LegalName).HasColumnName(LegalNameColumn);
        builder.Property(profile => profile.DateOfBirth).HasColumnName(DateOfBirthColumn);

        builder.HasOne<AccountRecord>()
            .WithOne()
            .HasForeignKey<ProfileRecord>(profile => profile.Subject)
            .HasConstraintName("fk_profiles_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
