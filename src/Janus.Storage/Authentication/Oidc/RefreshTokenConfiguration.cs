using System;
using Janus.Core;
using Janus.Storage.Authentication.Sessions;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How a refresh token is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-003 and AUTH-KEY-003. The family is indexed because a reuse
/// revokes every token of it in one statement, and the session is a foreign key
/// because a refresh token is a handle on a session record and never a credential of
/// its own.
/// </remarks>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshTokenRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "oidc_refresh_tokens";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RefreshTokenRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            Table,
            table => table.HasCheckConstraint("ck_oidc_refresh_tokens_expiry", "expires_at > issued_at"));

        builder.HasKey(token => token.Fingerprint).HasName("pk_oidc_refresh_tokens");

        builder.Property(token => token.Fingerprint).HasColumnName("fingerprint");

        builder.Property(token => token.Family)
            .HasColumnName("family")
            .HasConversion(family => family.Value, value => new RefreshFamilyId(value));

        builder.Property(token => token.ClientId).HasColumnName("client_id");

        builder.Property(token => token.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(token => token.Session)
            .HasColumnName("session")
            .HasConversion(session => session.Value, value => new SessionId(value));

        builder.Property(token => token.Scope).HasColumnName("scope");
        builder.Property(token => token.IssuedAt).HasColumnName("issued_at");
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.Property(token => token.ConsumedAt).HasColumnName("consumed_at");

        builder.HasIndex(token => token.Family).HasDatabaseName("ix_oidc_refresh_tokens_family");

        builder.HasIndex(token => token.ExpiresAt)
            .HasDatabaseName("ix_oidc_refresh_tokens_expires_at");

        builder.HasIndex(token => token.ClientId)
            .HasDatabaseName("ix_oidc_refresh_tokens_client_id");

        builder.HasIndex(token => token.Subject)
            .HasDatabaseName("ix_oidc_refresh_tokens_subject");

        builder.HasIndex(token => token.Session)
            .HasDatabaseName("ix_oidc_refresh_tokens_session");

        builder.HasOne<OidcClientRecord>()
            .WithMany()
            .HasForeignKey(token => token.ClientId)
            .HasConstraintName("fk_oidc_refresh_tokens_client_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(token => token.Subject)
            .HasConstraintName("fk_oidc_refresh_tokens_subject")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SessionRecord>()
            .WithMany()
            .HasForeignKey(token => token.Session)
            .HasConstraintName("fk_oidc_refresh_tokens_session")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
