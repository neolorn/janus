using System;
using System.Globalization;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// How an account's identifier is stored.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-004, IDN-ACCT-006, PRIV-RIGHT-005c, REG-SESS-005 and
/// CONV-DESIGN-003. One live fingerprint of a kind exists across the deployment,
/// because an identifier belongs to at most one account; a neutralised one is outside
/// the index, erasure leaving as many of those as there have been erasures.
/// </remarks>
internal sealed class IdentifierConfiguration : IEntityTypeConfiguration<IdentifierRecord>
{
    /// <summary>
    /// The table the ciphertext of this row is bound to.
    /// </summary>
    public const string Table = "identifiers";

    /// <summary>
    /// The column holding the form the person entered.
    /// </summary>
    public const string EnteredColumn = "enc_entered";

    /// <summary>
    /// The column holding the form it is compared under.
    /// </summary>
    public const string CanonicalColumn = "enc_canonical";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdentifierRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_identifiers_kind",
                Vocabulary.Admits<IdentifierKind>("kind"));

            table.HasCheckConstraint(
                "ck_identifiers_fingerprint",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"octet_length(fingerprint) = {Fingerprint.Length}"));

            // REG-IDENT-002: a username needs no verification and is verified when it is
            // chosen, so an unverified identifier is never the primary of its kind.
            table.HasCheckConstraint(
                "ck_identifiers_primary",
                "NOT is_primary OR verified_at IS NOT NULL");
        });

        builder.HasKey(identifier => identifier.Id).HasName("pk_identifiers");

        builder.Property(identifier => identifier.Id)
            .HasColumnName("identifier_id")
            .HasConversion(id => id.Value, value => new IdentifierId(value));

        builder.Property(identifier => identifier.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(identifier => identifier.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<IdentifierKind>());

        builder.Property(identifier => identifier.Fingerprint).HasColumnName("fingerprint");

        builder.Property(identifier => identifier.CanonicalisationVersion)
            .HasColumnName("canonicalisation_version");

        builder.Property(identifier => identifier.Entered).HasColumnName(EnteredColumn);
        builder.Property(identifier => identifier.Canonical).HasColumnName(CanonicalColumn);
        builder.Property(identifier => identifier.AddedAt).HasColumnName("added_at");
        builder.Property(identifier => identifier.VerifiedAt).HasColumnName("verified_at");
        builder.Property(identifier => identifier.IsPrimary).HasColumnName("is_primary");
        builder.Property(identifier => identifier.IsLocked).HasColumnName("is_locked");

        // IDN-PRIN-003: no account row is ever removed, so none is removed from under
        // the identifiers that reference it either.
        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(identifier => identifier.Subject)
            .HasConstraintName("fk_identifiers_subject")
            .OnDelete(DeleteBehavior.Restrict);

        // REG-SESS-005: an identifier that already belongs to another account lets no
        // second account hold it. Erasure neutralises rather than removes, so the
        // neutralised value is outside the index and repeats freely.
        builder.HasIndex(identifier => new { identifier.Kind, identifier.Fingerprint })
            .IsUnique()
            .HasDatabaseName("ux_identifiers_fingerprint")
            .HasFilter("fingerprint <> decode(repeat('00', " + Fingerprint.Length + "), 'hex')");

        // The set of one account's identifiers is read whole, per kind.
        builder.HasIndex(identifier => new { identifier.Subject, identifier.Kind })
            .HasDatabaseName("ix_identifiers_subject");
    }
}
