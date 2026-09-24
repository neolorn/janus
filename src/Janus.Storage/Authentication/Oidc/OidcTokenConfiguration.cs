using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How an issued code or token is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-003, AUTH-OIDC-004, AUTH-KEY-003 and CONV-ENUM-001. What the
/// holder presents is either the value itself or a reference to this row, and the
/// reference is unique because it is looked up by it. The status and the type are the
/// protocol server's vocabulary and carry no check constraint for the reason the
/// grants do not.
/// </remarks>
internal sealed class OidcTokenConfiguration : IEntityTypeConfiguration<OidcTokenRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "oidc_tokens";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OidcTokenRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(token => token.Id).HasName("pk_oidc_tokens");

        builder.Property(token => token.Id).HasColumnName("id");
        builder.Property(token => token.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(token => token.ApplicationId).HasColumnName("application_id");
        builder.Property(token => token.AuthorizationId).HasColumnName("authorization_id");

        builder.Property(token => token.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(token => token.Status).HasColumnName("status");
        builder.Property(token => token.Type).HasColumnName("type");
        builder.Property(token => token.ReferenceId).HasColumnName("reference_id");
        builder.Property(token => token.Payload).HasColumnName("payload");
        builder.Property(token => token.CreatedAt).HasColumnName("created_at");
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.Property(token => token.RedeemedAt).HasColumnName("redeemed_at");

        builder.Property(token => token.Properties)
            .HasColumnName("properties")
            .HasColumnType("jsonb");

        builder.HasIndex(token => token.ReferenceId)
            .IsUnique()
            .HasDatabaseName("ux_oidc_tokens_reference_id");

        builder.HasIndex(token => token.ApplicationId)
            .HasDatabaseName("ix_oidc_tokens_application_id");

        builder.HasIndex(token => token.AuthorizationId)
            .HasDatabaseName("ix_oidc_tokens_authorization_id");

        builder.HasIndex(token => token.Subject).HasDatabaseName("ix_oidc_tokens_subject");
        builder.HasIndex(token => token.ExpiresAt).HasDatabaseName("ix_oidc_tokens_expires_at");

        builder.HasOne<OidcClientRecord>()
            .WithMany()
            .HasForeignKey(token => token.ApplicationId)
            .HasConstraintName("fk_oidc_tokens_application_id")
            .OnDelete(DeleteBehavior.Cascade);

        // AUTH-OIDC-003 AC1: a reuse revokes the grant, and everything issued under it
        // is reached from the grant in one statement.
        builder.HasOne<OidcAuthorizationRecord>()
            .WithMany()
            .HasForeignKey(token => token.AuthorizationId)
            .HasConstraintName("fk_oidc_tokens_authorization_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(token => token.Subject)
            .HasConstraintName("fk_oidc_tokens_subject")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
