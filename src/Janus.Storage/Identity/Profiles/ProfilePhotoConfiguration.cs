using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Profiles;

/// <summary>
/// How an account's photo is stored.
/// </summary>
/// <remarks>
/// Implements IDN-ATTR-003, PRIV-RIGHT-005a and CONV-DESIGN-003. The bytes are in a
/// table of their own so that an ordinary read of an account never carries them.
/// </remarks>
internal sealed class ProfilePhotoConfiguration : IEntityTypeConfiguration<ProfilePhotoRecord>
{
    /// <summary>
    /// The table the encrypted column names as its location.
    /// </summary>
    public const string Table = "profile_photos";

    /// <summary>
    /// The column the image is written to.
    /// </summary>
    public const string ImageColumn = "enc_image";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ProfilePhotoRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(photo => photo.Subject).HasName("pk_profile_photos");

        builder.Property(photo => photo.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(photo => photo.Image).HasColumnName(ImageColumn);
        builder.Property(photo => photo.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<AccountRecord>()
            .WithOne()
            .HasForeignKey<ProfilePhotoRecord>(photo => photo.Subject)
            .HasConstraintName("fk_profile_photos_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
