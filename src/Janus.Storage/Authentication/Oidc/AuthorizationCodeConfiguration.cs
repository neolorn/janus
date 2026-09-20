using System;
using Janus.Core;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How an authorization code waiting to be exchanged is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012 and AUTH-KEY-003. The key is what the code hashes to, so
/// the table is looked up by a value the holder can present and holds nothing the
/// holder could be impersonated with.
/// </remarks>
internal sealed class AuthorizationCodeConfiguration : IEntityTypeConfiguration<AuthorizationCodeRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "oidc_codes";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuthorizationCodeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_oidc_codes_expiry", "expires_at > issued_at");

            // AUTH-SESS-012: the only proof key method the flow accepts. A row written
            // under any other would be exchangeable without the verifier.
            table.HasCheckConstraint("ck_oidc_codes_method", "challenge_method = 'S256'");
        });

        builder.HasKey(code => code.Fingerprint).HasName("pk_oidc_codes");

        builder.Property(code => code.Fingerprint).HasColumnName("fingerprint");
        builder.Property(code => code.ClientId).HasColumnName("client_id");

        builder.Property(code => code.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(code => code.Session)
            .HasColumnName("session")
            .HasConversion(session => session.Value, value => new SessionId(value));

        builder.Property(code => code.Redirect).HasColumnName("redirect");
        builder.Property(code => code.Challenge).HasColumnName("challenge");
        builder.Property(code => code.ChallengeMethod).HasColumnName("challenge_method");
        builder.Property(code => code.Scope).HasColumnName("scope");
        builder.Property(code => code.Nonce).HasColumnName("nonce");
        builder.Property(code => code.IssuedAt).HasColumnName("issued_at");
        builder.Property(code => code.ExpiresAt).HasColumnName("expires_at");
        builder.Property(code => code.SpentAt).HasColumnName("spent_at");

        builder.HasIndex(code => code.ExpiresAt).HasDatabaseName("ix_oidc_codes_expires_at");
        builder.HasIndex(code => code.ClientId).HasDatabaseName("ix_oidc_codes_client_id");
        builder.HasIndex(code => code.Subject).HasDatabaseName("ix_oidc_codes_subject");
        builder.HasIndex(code => code.Session).HasDatabaseName("ix_oidc_codes_session");

        builder.HasOne<OidcClientRecord>()
            .WithMany()
            .HasForeignKey(code => code.ClientId)
            .HasConstraintName("fk_oidc_codes_client_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(code => code.Subject)
            .HasConstraintName("fk_oidc_codes_subject")
            .OnDelete(DeleteBehavior.Cascade);

        // AUTH-SESS-012: the code stands on the session record the token will be minted
        // from, so it cannot outlive it.
        builder.HasOne<SessionRecord>()
            .WithMany()
            .HasForeignKey(code => code.Session)
            .HasConstraintName("fk_oidc_codes_session")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
