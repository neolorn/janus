using System;
using Janus.Authentication.Accounts;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Accounts;

/// <summary>
/// How the link an account's lifecycle notice carried is stored.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-013, IDN-LIFE-014 and CONV-ENUM-001. One link per account,
/// which the unique index holds rather than a read before a write: an account is
/// either suspended or deleting and never both.
/// </remarks>
internal sealed class LifecycleLinkConfiguration : IEntityTypeConfiguration<LifecycleLinkRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "lifecycle_links";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LifecycleLinkRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_lifecycle_links_token",
                $"octet_length(token) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_lifecycle_links_kind",
                Vocabulary.Admits<LifecycleLinkKind>("kind"));
        });

        builder.HasKey(link => link.Token).HasName("pk_lifecycle_links");

        builder.Property(link => link.Token)
            .HasColumnName("token")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(link => link.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(link => link.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<LifecycleLinkKind>());

        builder.Property(link => link.IssuedAt).HasColumnName("issued_at");

        builder.HasIndex(link => link.Subject)
            .HasDatabaseName("ux_lifecycle_links_subject")
            .IsUnique();

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(link => link.Subject)
            .HasConstraintName("fk_lifecycle_links_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
