using System;
using System.Globalization;
using Janus.Core;
using Janus.Identity.Identifiers;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// How an identifier an account gave up is stored.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-006 and CONV-DESIGN-003. The fingerprint is unique across the
/// table and does not meet the identifiers table's index, so the value is held out of
/// reach of another account while the undo lasts and released with the row. Each row
/// is keyed by an identifier of its own and names the identifier it came from, so one
/// identifier changed twice within the window stands behind two rows (D-189).
/// </remarks>
internal sealed class IdentifierRemovalConfiguration : IEntityTypeConfiguration<IdentifierRemovalRecord>
{
    /// <summary>
    /// The table the ciphertext of this row is bound to.
    /// </summary>
    public const string Table = "identifier_removals";

    /// <summary>
    /// The column holding the form the person entered.
    /// </summary>
    public const string EnteredColumn = "enc_entered";

    /// <summary>
    /// The column holding the form it is compared under.
    /// </summary>
    public const string CanonicalColumn = "enc_canonical";

    /// <summary>
    /// The column naming the identifier the value came from.
    /// </summary>
    public const string OriginColumn = "identifier_id";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdentifierRemovalRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_identifier_removals_kind",
                Vocabulary.Admits<IdentifierKind>("kind"));

            table.HasCheckConstraint(
                "ck_identifier_removals_fingerprint",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"octet_length(fingerprint) = {Fingerprint.Length}"));

            // REG-IDENT-006: the undo runs from the removal, so a window that ended
            // before it began would be a row no undo could ever use.
            table.HasCheckConstraint(
                "ck_identifier_removals_window",
                "expires_at > removed_at");

            table.HasCheckConstraint(
                "ck_identifier_removals_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(removal => removal.Id).HasName("pk_identifier_removals");

        builder.Property(removal => removal.Id)
            .HasColumnName("removal_id")
            .HasConversion(id => id.Value, value => new IdentifierRemovalId(value));

        // REG-IDENT-006 (D-189): the identifier the value came from is named and is no
        // key, so one identifier changed twice within the window stands behind two rows.
        builder.Property(removal => removal.Origin)
            .HasColumnName(OriginColumn)
            .HasConversion(id => id.Value, value => new IdentifierId(value));

        builder.Property(removal => removal.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(removal => removal.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<IdentifierKind>());

        builder.Property(removal => removal.Fingerprint).HasColumnName("fingerprint");
        builder.Property(removal => removal.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(removal => removal.Entered).HasColumnName(EnteredColumn);
        builder.Property(removal => removal.Canonical).HasColumnName(CanonicalColumn);
        builder.Property(removal => removal.IsLocked).HasColumnName("is_locked");
        builder.Property(removal => removal.AddedAt).HasColumnName("added_at");
        builder.Property(removal => removal.VerifiedAt).HasColumnName("verified_at");
        builder.Property(removal => removal.RemovedAt).HasColumnName("removed_at");
        builder.Property(removal => removal.ExpiresAt).HasColumnName("expires_at");
        builder.Property(removal => removal.Undo).HasColumnName("undo_fingerprint");

        // IDN-PRIN-003: no account row is ever removed, so none is removed from under
        // the removals that reference it either.
        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(removal => removal.Subject)
            .HasConstraintName("fk_identifier_removals_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(removal => new { removal.Kind, removal.Fingerprint })
            .IsUnique()
            .HasDatabaseName("ux_identifier_removals_fingerprint");

        // The undo link resolves through the token it carries and nothing else.
        builder.HasIndex(removal => removal.Undo)
            .IsUnique()
            .HasDatabaseName("ux_identifier_removals_undo");

        // The sweep reads the windows that have run out and nothing else.
        builder.HasIndex(removal => removal.ExpiresAt)
            .HasDatabaseName("ix_identifier_removals_expires_at");

        // The fingerprint rotation rewrites a row it names by the identifier it came
        // from, which is no longer the key that served it (OPS-SEC-003).
        builder.HasIndex(removal => removal.Origin)
            .HasDatabaseName("ix_identifier_removals_identifier");

        // An account reads what it gave up, and the foreign key is served by the same
        // index rather than one the framework names for itself.
        builder.HasIndex(removal => removal.Subject)
            .HasDatabaseName("ix_identifier_removals_subject");
    }
}
