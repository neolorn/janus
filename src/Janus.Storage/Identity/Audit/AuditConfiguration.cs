using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Audit;

/// <summary>
/// How an audit record is stored.
/// </summary>
/// <remarks>
/// Implements IDN-AUD-001, PRIV-RET-002, PRIV-RET-003 and CONV-DESIGN-003. The table is
/// partitioned by category and then by calendar month, which no model builder can
/// express, so the migration writes its definition and this mapping is excluded from
/// migrations; the two have to be read together. There is no foreign key on either
/// identity column, because a system principal acts under an identity that holds no
/// account row (IDN-PRIN-001), and no row referenced here is ever removed anyway
/// (IDN-PRIN-003).
/// </remarks>
internal sealed class AuditConfiguration : IEntityTypeConfiguration<AuditRowRecord>
{
    /// <summary>
    /// The table the encrypted column names as its location.
    /// </summary>
    public const string Table = "audit_records";

    /// <summary>
    /// The column the attributes an event records are written to.
    /// </summary>
    public const string PersonalDetailsColumn = "enc_details";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditRowRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table => table.ExcludeFromMigrations());

        // The partition key is part of every key of a partitioned table.
        builder.HasKey(record => new { record.Category, record.OccurredAt, record.Id })
            .HasName("pk_audit_records");

        builder.Property(record => record.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new AuditRecordId(value));

        builder.Property(record => record.Category)
            .HasColumnName("category")
            .HasConversion(new VocabularyConverter<AuditCategory>());

        builder.Property(record => record.OccurredAt).HasColumnName("occurred_at");

        builder.Property(record => record.Action)
            .HasColumnName("action")
            .HasConversion(action => action.ToString(), value => AuditAction.Parse(value));

        builder.Property(record => record.ActingSubject)
            .HasColumnName("acting_subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(record => record.EffectiveSubject)
            .HasColumnName("effective_subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(record => record.Organization)
            .HasColumnName("organization")
            .HasConversion(
                id => id!.Value.Value,
                value => new OrganizationId(value));

        builder.Property(record => record.Details)
            .HasColumnName("details")
            .HasColumnType("jsonb");

        builder.Property(record => record.PersonalDetails).HasColumnName(PersonalDetailsColumn);

        // PRIV-BREACH-002: every record of one subject, without a full scan.
        builder.HasIndex(record => new { record.EffectiveSubject, record.OccurredAt })
            .HasDatabaseName("ix_audit_records_effective_subject");
    }
}
