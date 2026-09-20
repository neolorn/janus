using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Accounts;

/// <summary>
/// How an account is stored.
/// </summary>
/// <remarks>
/// Implements IDN-ACCT-007, IDN-LIFE-003, IDN-LIFE-013, CONV-ENUM-001 and
/// CONV-DESIGN-003. The three
/// vocabularies are constrained columns rather than native enum types, so a value the
/// code does not branch on is refused by the database and the list changes freely.
/// </remarks>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<AccountRecord>
{
    private const int DocumentVersionLength = 64;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AccountRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("accounts", table =>
        {
            table.HasCheckConstraint(
                "ck_accounts_state",
                Vocabulary.Admits<AccountState>("state"));
            table.HasCheckConstraint(
                "ck_accounts_suspended_by",
                "suspended_by IS NULL OR "
                    + Vocabulary.Admits<SuspensionOrigin>("suspended_by"));
            table.HasCheckConstraint(
                "ck_accounts_deleting_by",
                "deleting_by IS NULL OR "
                    + Vocabulary.Admits<DeletionOrigin>("deleting_by"));

            // IDN-ACCT-007: a grace window that is running has an instant it began, and
            // one that is not has neither an origin nor an instant.
            table.HasCheckConstraint(
                "ck_accounts_deleting",
                "(deleting_by IS NULL) = (deleting_since IS NULL)");
            table.HasCheckConstraint(
                "ck_accounts_age_group",
                "age_group IS NULL OR " + Vocabulary.Admits<AgeGroup>("age_group"));

            // REG-PROF-002 AC3 and AC4: the age screen records an affirmation or a
            // band, never both, and its instant comes with whichever it recorded.
            table.HasCheckConstraint(
                "ck_accounts_age_answer",
                "adult_affirmed IS NULL OR age_group IS NULL");
            table.HasCheckConstraint(
                "ck_accounts_answered_age_at",
                "(answered_age_at IS NULL) = "
                    + "(adult_affirmed IS NULL AND age_group IS NULL)");

            // REG-SESS-007 AC2: an account a registration created carries both
            // documents, and one no registration created carries neither.
            table.HasCheckConstraint(
                "ck_accounts_documents",
                "(terms_version IS NULL) = (notice_version IS NULL)");
        });

        builder.HasKey(account => account.Subject).HasName("pk_accounts");

        builder.Property(account => account.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(account => account.CreatedAt).HasColumnName("created_at");

        builder.Property(account => account.State)
            .HasColumnName("state")
            .HasConversion(new VocabularyConverter<AccountState>());

        builder.Property(account => account.SuspendedBy)
            .HasColumnName("suspended_by")
            .HasConversion(new VocabularyConverter<SuspensionOrigin>());

        builder.Property(account => account.DeletingBy)
            .HasColumnName("deleting_by")
            .HasConversion(new VocabularyConverter<DeletionOrigin>());

        builder.Property(account => account.DeletingSince).HasColumnName("deleting_since");

        builder.Property(account => account.AdultAffirmed).HasColumnName("adult_affirmed");

        builder.Property(account => account.AgeGroup)
            .HasColumnName("age_group")
            .HasConversion(new VocabularyConverter<AgeGroup>());

        builder.Property(account => account.AnsweredAgeAt).HasColumnName("answered_age_at");

        builder.Property(account => account.TermsVersion)
            .HasColumnName("terms_version")
            .HasMaxLength(DocumentVersionLength);

        builder.Property(account => account.NoticeVersion)
            .HasColumnName("notice_version")
            .HasMaxLength(DocumentVersionLength);

        // The sweep of OPS-OBS-003 reads the windows that have elapsed and nothing else.
        builder.HasIndex(account => account.DeletingSince)
            .HasDatabaseName("ix_accounts_deleting_since")
            .HasFilter("deleting_since IS NOT NULL");
    }

}
