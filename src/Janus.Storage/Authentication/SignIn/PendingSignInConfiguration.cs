using System;
using System.Linq;
using Janus.Authentication.SignIn;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.SignIn;

/// <summary>
/// How a sign-in link or code that has gone out is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-003 and CONV-ENUM-001. One is outstanding per account per
/// catalogue entry, which the unique index holds rather than a read before a write:
/// asking again replaces what went before, so an older message is never a second way
/// in. A second step's code names the credential it was issued for and no other row
/// names one (AUTH-FACT-004); no foreign key holds the name, since a code outlives a
/// credential removed since it was sent and is refused by its absence.
/// </remarks>
internal sealed class PendingSignInConfiguration : IEntityTypeConfiguration<PendingSignInRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "signin_links";

    /// <summary>The column the code sent beside the link is held in.</summary>
    public const string CodeColumn = "enc_code";

    // AUTH-FACT-004: the spellings of the entries whose code names a credential.
    private static readonly string[] NamingCredential =
    [
        .. Enum.GetValues<Factor>()
            .Where(PendingSignIn.NamesCredential)
            .Select(VocabularyConverter<Factor>.Write)
            .Order(StringComparer.Ordinal),
    ];

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PendingSignInRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_signin_links_token",
                $"octet_length(token) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_signin_links_browser",
                $"browser IS NULL OR octet_length(browser) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_signin_links_challenge",
                $"challenge IS NULL OR octet_length(challenge) = {Fingerprint.Length}");
            table.HasCheckConstraint(
                "ck_signin_links_factor",
                Vocabulary.Admits<Factor>("factor"));
            table.HasCheckConstraint("ck_signin_links_wrong_attempts", "wrong_attempts >= 0");
            table.HasCheckConstraint(
                "ck_signin_links_credential",
                $"(credential IS NOT NULL) = ({Vocabulary.Admits("factor", NamingCredential)})");

            table.HasCheckConstraint(
                "ck_signin_links_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
        });

        builder.HasKey(pending => pending.Token).HasName("pk_signin_links");

        builder.Property(pending => pending.Token)
            .HasColumnName("token")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(pending => pending.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(pending => pending.Factor)
            .HasColumnName("factor")
            .HasConversion(new VocabularyConverter<Factor>());

        builder.Property(pending => pending.Email)
            .HasColumnName("email")
            .HasConversion(email => email!.Value.Value, value => new IdentifierId(value));

        builder.Property(pending => pending.Credential)
            .HasColumnName("credential")
            .HasConversion(credential => credential!.Value.Value, value => new AuthenticatorId(value));

        builder.Property(pending => pending.Code).HasColumnName(CodeColumn);

        builder.Property(pending => pending.Browser)
            .HasColumnName("browser")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(pending => pending.Challenge)
            .HasColumnName("challenge")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(pending => pending.IssuedAt).HasColumnName("issued_at");
        builder.Property(pending => pending.ExpiresAt).HasColumnName("expires_at");
        builder.Property(pending => pending.WrongAttempts).HasColumnName("wrong_attempts");

        builder.HasIndex(pending => new { pending.Subject, pending.Factor })
            .HasDatabaseName("ux_signin_links_subject_factor")
            .IsUnique();

        builder.HasIndex(pending => pending.ExpiresAt)
            .HasDatabaseName("ix_signin_links_expires_at");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(pending => pending.Subject)
            .HasConstraintName("fk_signin_links_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
