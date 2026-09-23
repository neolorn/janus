using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Mailboxes;

/// <summary>
/// How a mailbox the library provisions is stored.
/// </summary>
/// <remarks>
/// Implements INT-MAIL-006, INT-MAIL-007, PRIV-RIGHT-005a and PRIV-RIGHT-005c. One row
/// per address, for good, which the unique index on the fingerprint holds: an address
/// is one mailbox on the server however many invitations reserve it over the years. An
/// outstanding push always carries its key, and a row nobody holds carries a key of
/// its own.
/// </remarks>
internal sealed class MailboxConfiguration : IEntityTypeConfiguration<MailboxRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "mailboxes";

    /// <summary>The column the address is encrypted in, in its canonical form.</summary>
    public const string CanonicalColumn = "enc_canonical";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MailboxRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_mailboxes_pushed",
                "pushed IS NULL OR " + Vocabulary.Admits<MailboxState>("pushed"));
            table.HasCheckConstraint(
                "ck_mailboxes_pending",
                "pending IS NULL OR " + Vocabulary.Admits<MailboxState>("pending"));
            table.HasCheckConstraint(
                "ck_mailboxes_pending_key",
                "(pending IS NULL) = (pending_key IS NULL)");
            table.HasCheckConstraint(
                "ck_mailboxes_released",
                "released_at IS NULL OR (holder IS NULL AND retired_at IS NULL)");
            table.HasCheckConstraint(
                "ck_mailboxes_key",
                "(holder IS NULL) = (wrapped_key IS NOT NULL) AND (wrapped_key IS NULL) = (key_version IS NULL)");
        });

        builder.HasKey(mailbox => mailbox.Id).HasName("pk_mailboxes");

        builder.Property(mailbox => mailbox.Id).HasColumnName("id");
        builder.Property(mailbox => mailbox.Fingerprint).HasColumnName("fingerprint");

        builder.Property(mailbox => mailbox.CanonicalisationVersion)
            .HasColumnName("canonicalisation_version");

        builder.Property(mailbox => mailbox.EncryptedCanonical).HasColumnName(CanonicalColumn);
        builder.Property(mailbox => mailbox.KeyVersion).HasColumnName("key_version");
        builder.Property(mailbox => mailbox.WrappedKey).HasColumnName("wrapped_key");
        builder.Property(mailbox => mailbox.ReservedAt).HasColumnName("reserved_at");

        builder.Property(mailbox => mailbox.Holder)
            .HasColumnName("holder")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(mailbox => mailbox.RetiredAt).HasColumnName("retired_at");
        builder.Property(mailbox => mailbox.ReleasedAt).HasColumnName("released_at");

        builder.Property(mailbox => mailbox.Pushed)
            .HasColumnName("pushed")
            .HasConversion(new VocabularyConverter<MailboxState>());

        builder.Property(mailbox => mailbox.Pending)
            .HasColumnName("pending")
            .HasConversion(new VocabularyConverter<MailboxState>());

        builder.Property(mailbox => mailbox.PendingKey).HasColumnName("pending_key");
        builder.Property(mailbox => mailbox.Attempts).HasColumnName("attempts");
        builder.Property(mailbox => mailbox.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(mailbox => mailbox.FailedAt).HasColumnName("failed_at");

        // PRIV-RIGHT-005c: a fingerprint erasure neutralised is nobody's address, and
        // several may stand side by side.
        builder.HasIndex(mailbox => mailbox.Fingerprint)
            .HasDatabaseName("ux_mailboxes_fingerprint")
            .IsUnique()
            .HasFilter("fingerprint <> decode(repeat('00', " + Janus.Storage.Fingerprint.Length + "), 'hex')");

        builder.HasIndex(mailbox => mailbox.Holder)
            .HasDatabaseName("ix_mailboxes_holder");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(mailbox => mailbox.Holder)
            .HasConstraintName("fk_mailboxes_holder")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
