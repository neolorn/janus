using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How a grant made to a client is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001, AUTH-OIDC-003 and CONV-ENUM-001. The status and the type
/// carry no check constraint: the library branches on neither and writes neither, so
/// constraining a vocabulary the protocol server owns would turn an upgrade of it into
/// a schema migration.
/// </remarks>
internal sealed class OidcAuthorizationConfiguration
    : IEntityTypeConfiguration<OidcAuthorizationRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "oidc_authorizations";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OidcAuthorizationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(authorization => authorization.Id).HasName("pk_oidc_authorizations");

        builder.Property(authorization => authorization.Id).HasColumnName("id");
        builder.Property(authorization => authorization.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(authorization => authorization.ApplicationId).HasColumnName("application_id");

        builder.Property(authorization => authorization.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(authorization => authorization.Status).HasColumnName("status");
        builder.Property(authorization => authorization.Type).HasColumnName("type");
        builder.Property(authorization => authorization.Scopes).HasColumnName("scopes");
        builder.Property(authorization => authorization.CreatedAt).HasColumnName("created_at");

        builder.Property(authorization => authorization.Properties)
            .HasColumnName("properties")
            .HasColumnType("jsonb");

        builder.HasIndex(authorization => authorization.ApplicationId)
            .HasDatabaseName("ix_oidc_authorizations_application_id");

        builder.HasIndex(authorization => authorization.Subject)
            .HasDatabaseName("ix_oidc_authorizations_subject");

        builder.HasIndex(authorization => authorization.CreatedAt)
            .HasDatabaseName("ix_oidc_authorizations_created_at");

        builder.HasOne<OidcClientRecord>()
            .WithMany()
            .HasForeignKey(authorization => authorization.ApplicationId)
            .HasConstraintName("fk_oidc_authorizations_application_id")
            .OnDelete(DeleteBehavior.Cascade);

        // PRIV-ERASE-001: what the account leaves behind goes with the account.
        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(authorization => authorization.Subject)
            .HasConstraintName("fk_oidc_authorizations_subject")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
