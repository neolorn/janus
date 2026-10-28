using System;
using System.Globalization;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// How a person record's contact is stored.
/// </summary>
/// <remarks>
/// Implements REC-REC-001, PRIV-RIGHT-005c and CONV-DESIGN-003. A record holds at most
/// one email and one phone, the key of the row saying so. A value is not unique across
/// records: two organizations, or two collections a proof had aged out of, may each hold
/// it, so its fingerprint is indexed and not constrained.
/// </remarks>
internal sealed class RecordContactConfiguration : IEntityTypeConfiguration<RecordContactRow>
{
    /// <summary>
    /// The table the ciphertext of this row is bound to.
    /// </summary>
    public const string Table = "person_record_contacts";

    /// <summary>
    /// The column holding the canonical form.
    /// </summary>
    public const string ValueColumn = "enc_value";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecordContactRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            // REC-REC-001: a record's contacts are an email and a phone, never a username.
            table.HasCheckConstraint(
                "ck_person_record_contacts_kind",
                Vocabulary.Admits(
                    "kind",
                    [
                        VocabularyConverter<IdentifierKind>.Write(IdentifierKind.Email),
                        VocabularyConverter<IdentifierKind>.Write(IdentifierKind.Phone),
                    ]));

            table.HasCheckConstraint(
                "ck_person_record_contacts_fingerprint",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"octet_length(fingerprint) = {Fingerprint.Length}"));

            table.HasCheckConstraint(
                "ck_person_record_contacts_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(contact => new { contact.Subject, contact.Kind })
            .HasName("pk_person_record_contacts");

        builder.Property(contact => contact.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(contact => contact.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<IdentifierKind>());

        builder.Property(contact => contact.Fingerprint).HasColumnName("fingerprint");
        builder.Property(contact => contact.FingerprintVersion).HasColumnName("fingerprint_version");

        builder.Property(contact => contact.CanonicalisationVersion)
            .HasColumnName("canonicalisation_version");

        builder.Property(contact => contact.Value).HasColumnName(ValueColumn);
        builder.Property(contact => contact.VerifiedAt).HasColumnName("verified_at");

        builder.HasOne<PersonRecordRow>()
            .WithMany()
            .HasForeignKey(contact => contact.Subject)
            .HasConstraintName("fk_person_record_contacts_subject")
            .OnDelete(DeleteBehavior.Restrict);

        // REC-COL-002, REC-ACC-001, REC-ATT-001: the records holding a value are found
        // by its fingerprint; a neutralised one is outside the index.
        builder.HasIndex(contact => new { contact.Kind, contact.Fingerprint })
            .HasDatabaseName("ix_person_record_contacts_fingerprint")
            .HasFilter("fingerprint <> decode(repeat('00', " + Fingerprint.Length + "), 'hex')");
    }
}
