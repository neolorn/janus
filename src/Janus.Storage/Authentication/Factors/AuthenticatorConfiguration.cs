using System;
using System.Globalization;
using System.Linq;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// How an enrolled credential is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-001, AUTH-FACT-006, AUTH-FACT-011, IDN-LIFE-012a and
/// CONV-ENUM-001. A label is unique per kind per account, and a provider's subject per
/// provider, which the database holds rather than a read before a write.
/// </remarks>
internal sealed class AuthenticatorConfiguration : IEntityTypeConfiguration<AuthenticatorRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "authenticators";

    /// <summary>The column the shared secret of a code generator is held in.</summary>
    public const string TotpSecretColumn = "totp_secret";

    /// <summary>The column a linked provider's subject is held in.</summary>
    public const string ProviderSubjectColumn = "enc_provider_subject";

    private const int LabelLength = 64;

    // The entries of the catalogue a person links rather than enrols: the social
    // providers, which assert a credential and no tier.
    private static readonly string[] Linked =
    [
        .. FactorCatalogue.Entries
            .Where(entry => entry.Value.AssuranceLevel is AssuranceLevel.Delegated)
            .Select(entry => VocabularyConverter<Factor>.Write(entry.Key))
            .Order(StringComparer.Ordinal),
    ];

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuthenticatorRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_authenticators_factor",
                Vocabulary.Admits<Factor>("factor"));
            table.HasCheckConstraint(
                "ck_authenticators_state",
                Vocabulary.Admits<AuthenticatorState>("state"));

            // AUTH-RECOV-007: a credential is invalidated at a stated instant only
            // while it is suspended, and stands at no instant otherwise.
            table.HasCheckConstraint(
                "ck_authenticators_invalidates_at",
                "invalidates_at IS NULL OR "
                    + Vocabulary.Admits(
                        "state",
                        [VocabularyConverter<AuthenticatorState>.Write(AuthenticatorState.Suspended)]));

            // AUTH-FACT-011: a credential that holds a key holds all of it.
            table.HasCheckConstraint(
                "ck_authenticators_webauthn",
                "(credential_id IS NULL) = (public_key IS NULL) AND "
                    + "(credential_id IS NULL) = (algorithm IS NULL) AND "
                    + "(credential_id IS NULL) = (relying_party IS NULL) AND "
                    + "(credential_id IS NULL) = (backup_eligible IS NULL) AND "
                    + "(credential_id IS NULL) = (backup_state IS NULL)");

            // IDN-LIFE-012a, PRIV-RIGHT-005c: a linked identity is found by the keyed
            // fingerprint of the provider's subject, and only a linked identity has one,
            // with the version it was computed under and the subject it was computed
            // from (OPS-SEC-003).
            table.HasCheckConstraint(
                "ck_authenticators_provider_subject",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"(provider_subject IS NULL) <> ({Vocabulary.Admits("factor", Linked)}) AND "
                        + $"(provider_subject IS NULL OR octet_length(provider_subject) = {Fingerprint.Length}) AND "
                        + $"(provider_subject IS NULL) = (fingerprint_version IS NULL) AND "
                        + $"(provider_subject IS NULL) = ({ProviderSubjectColumn} IS NULL)"));
        });

        builder.HasKey(credential => credential.Id).HasName("pk_authenticators");

        builder.Property(credential => credential.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new AuthenticatorId(value));

        builder.Property(credential => credential.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(credential => credential.Factor)
            .HasColumnName("factor")
            .HasConversion(new VocabularyConverter<Factor>());

        builder.Property(credential => credential.Label)
            .HasColumnName("label")
            .HasMaxLength(LabelLength);

        builder.Property(credential => credential.State)
            .HasColumnName("state")
            .HasConversion(new VocabularyConverter<AuthenticatorState>());

        builder.Property(credential => credential.AddedAt).HasColumnName("added_at");
        builder.Property(credential => credential.LastUsedAt).HasColumnName("last_used_at");
        builder.Property(credential => credential.InvalidatesAt).HasColumnName("invalidates_at");
        builder.Property(credential => credential.Confirmed).HasColumnName("confirmed");
        builder.Property(credential => credential.TotpSecret).HasColumnName(TotpSecretColumn);
        builder.Property(credential => credential.TotpConsumedStep).HasColumnName("totp_consumed_step");
        builder.Property(credential => credential.CredentialId).HasColumnName("credential_id");
        builder.Property(credential => credential.PublicKey).HasColumnName("public_key");
        builder.Property(credential => credential.Algorithm).HasColumnName("algorithm");
        builder.Property(credential => credential.RelyingParty).HasColumnName("relying_party");
        builder.Property(credential => credential.Counter).HasColumnName("counter");
        builder.Property(credential => credential.BackupEligible).HasColumnName("backup_eligible");
        builder.Property(credential => credential.BackupState).HasColumnName("backup_state");
        builder.Property(credential => credential.IsPreferred).HasColumnName("is_preferred");
        builder.Property(credential => credential.ProviderSubject).HasColumnName("provider_subject");
        builder.Property(credential => credential.FingerprintVersion).HasColumnName("fingerprint_version");
        builder.Property(credential => credential.EncryptedProviderSubject).HasColumnName(ProviderSubjectColumn);

        // AUTH-FACT-013: the browser names the credential and not the account, so the
        // credential identifier is what a presentation is resolved by.
        builder.HasIndex(credential => credential.CredentialId)
            .HasDatabaseName("ux_authenticators_credential_id")
            .IsUnique()
            .HasFilter("credential_id IS NOT NULL");

        // REG-IDENT-008: a provider's subject is linked to one account. A fingerprint
        // erasure neutralised is nobody's, so the rows it leaves do not collide.
        builder.HasIndex(credential => new
        {
            credential.Factor,
            credential.ProviderSubject,
        })
            .HasDatabaseName("ux_authenticators_provider_subject")
            .IsUnique()
            .HasFilter(
                "provider_subject IS NOT NULL AND provider_subject <> decode(repeat('00', "
                    + Fingerprint.Length.ToString(CultureInfo.InvariantCulture)
                    + "), 'hex')");

        // AUTH-FACT-001 AC5: a label is held once per kind per account.
        builder.HasIndex(credential => new
        {
            credential.Subject,
            credential.Factor,
            credential.Label,
        })
            .HasDatabaseName("ux_authenticators_label")
            .IsUnique();

        // IDN-ATTR-008: an account marks at most one credential to be offered first.
        builder.HasIndex(credential => credential.Subject)
            .HasDatabaseName("ux_authenticators_preferred")
            .IsUnique()
            .HasFilter("is_preferred");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(credential => credential.Subject)
            .HasConstraintName("fk_authenticators_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
