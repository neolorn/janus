using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Identifiers;

/// <summary>
/// How a kind's backup setting is stored.
/// </summary>
/// <remarks>
/// Implements REG-IDENT-002, chapter 10 section 5.17 and CONV-DESIGN-003. A kind the
/// account has never changed has no row and reads as the default the chapter gives it.
/// </remarks>
internal sealed class BackupSettingConfiguration : IEntityTypeConfiguration<BackupSettingRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BackupSettingRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("identifier_backup_settings", table =>
        {
            table.HasCheckConstraint(
                "ck_identifier_backup_settings_kind",
                Vocabulary.Admits<IdentifierKind>("kind"));

            table.HasCheckConstraint(
                "ck_identifier_backup_settings_rule",
                Vocabulary.Admits(
                    "rule",
                    [BackupSettingRecord.AllVerified, BackupSettingRecord.PrimaryOnly])
                    + " OR rule IS NULL");

            // Chapter 10 section 5.17: the setting is one of the two words or the
            // identifier of one named verified identifier, and never both or neither.
            table.HasCheckConstraint(
                "ck_identifier_backup_settings_setting",
                "(rule IS NULL) <> (named IS NULL)");
        });

        builder.HasKey(setting => new { setting.Subject, setting.Kind })
            .HasName("pk_identifier_backup_settings");

        builder.Property(setting => setting.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(setting => setting.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<IdentifierKind>());

        builder.Property(setting => setting.Rule).HasColumnName("rule");

        builder.Property(setting => setting.Named)
            .HasColumnName("named")
            .HasConversion(
                named => named!.Value.Value,
                value => new IdentifierId(value));

        // IDN-PRIN-003: no row this one references is ever removed.
        builder.HasOne<Accounts.AccountRecord>()
            .WithMany()
            .HasForeignKey(setting => setting.Subject)
            .HasConstraintName("fk_identifier_backup_settings_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IdentifierRecord>()
            .WithMany()
            .HasForeignKey(setting => setting.Named)
            .HasConstraintName("fk_identifier_backup_settings_named")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(setting => setting.Named)
            .HasDatabaseName("ix_identifier_backup_settings_named");
    }
}
