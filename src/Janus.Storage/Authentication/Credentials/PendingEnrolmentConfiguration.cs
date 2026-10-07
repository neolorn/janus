using System;
using System.Linq;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Credentials;

/// <summary>
/// How a second step the library texts is stored while its code has yet to confirm it.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-002 and CONV-ENUM-001. One is pending per account per catalogue
/// entry, which the key holds rather than a read before a write: staging again replaces
/// what went before, so an older code confirms nothing.
/// </remarks>
internal sealed class PendingEnrolmentConfiguration : IEntityTypeConfiguration<PendingEnrolmentRecord>
{
    /// <summary>The table, which each encrypted column names as its location.</summary>
    public const string Table = "pending_enrolments";

    /// <summary>The column the number the credential would be bound to is held in.</summary>
    public const string NumberColumn = "enc_number";

    /// <summary>The column the code texted to that number is held in.</summary>
    public const string CodeColumn = "enc_code";

    private const int LabelLength = 64;

    // The entries of the catalogue a texted code enrols: those whose credential is
    // bound to a number.
    private static readonly string[] Bound =
    [
        .. FactorCatalogue.Entries.Keys
            .Where(Authenticator.IsBoundToNumber)
            .Select(VocabularyConverter<Factor>.Write)
            .Order(StringComparer.Ordinal),
    ];

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PendingEnrolmentRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_pending_enrolments_factor",
                Vocabulary.Admits("factor", Bound));
            table.HasCheckConstraint(
                "ck_pending_enrolments_send",
                $"octet_length(send) = {Fingerprint.Length}");
            table.HasCheckConstraint("ck_pending_enrolments_wrong_attempts", "wrong_attempts >= 0");
            table.HasCheckConstraint(
                "ck_pending_enrolments_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(pending => new { pending.Subject, pending.Factor }).HasName("pk_pending_enrolments");

        builder.Property(pending => pending.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(pending => pending.Factor)
            .HasColumnName("factor")
            .HasConversion(new VocabularyConverter<Factor>());

        builder.Property(pending => pending.Label)
            .HasColumnName("label")
            .HasMaxLength(LabelLength);

        builder.Property(pending => pending.Number).HasColumnName(NumberColumn);

        builder.Property(pending => pending.Send)
            .HasColumnName("send")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(pending => pending.Code).HasColumnName(CodeColumn);
        builder.Property(pending => pending.IssuedAt).HasColumnName("issued_at");
        builder.Property(pending => pending.ExpiresAt).HasColumnName("expires_at");
        builder.Property(pending => pending.WrongAttempts).HasColumnName("wrong_attempts");

        builder.HasIndex(pending => pending.ExpiresAt)
            .HasDatabaseName("ix_pending_enrolments_expires_at");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(pending => pending.Subject)
            .HasConstraintName("fk_pending_enrolments_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
