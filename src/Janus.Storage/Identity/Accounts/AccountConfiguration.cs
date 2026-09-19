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

        // The sweep of OPS-OBS-003 reads the windows that have elapsed and nothing else.
        builder.HasIndex(account => account.DeletingSince)
            .HasDatabaseName("ix_accounts_deleting_since")
            .HasFilter("deleting_since IS NOT NULL");
    }

}
