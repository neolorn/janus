using System;
using Janus.Core;
using Janus.Storage.Authentication.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How an authorization request waiting for its sign-in is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-SESS-012 (D-224, D-226). A browser has one at most and so has a
/// session record, which the database holds rather than a read before a write. Ending
/// the pre-authentication session or the session record it is bound to ends it with
/// them.
/// </remarks>
internal sealed class PendingAuthorizationConfiguration
    : IEntityTypeConfiguration<PendingAuthorizationRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "pending_authorizations";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PendingAuthorizationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_pending_authorizations_bound",
                "num_nonnulls(preauthentication, session) = 1");
            table.HasCheckConstraint(
                "ck_pending_authorizations_expires_at",
                "expires_at > created_at");
        });

        builder.HasKey(pending => pending.Id).HasName("pk_pending_authorizations");

        builder.Property(pending => pending.Id).HasColumnName("id");

        builder.Property(pending => pending.PreAuthentication)
            .HasColumnName("preauthentication")
            .HasMaxLength(Fingerprint.Length);

        builder.Property(pending => pending.Session)
            .HasColumnName("session")
            .HasConversion(id => id!.Value.Value, value => new SessionId(value));

        builder.Property(pending => pending.Parameters).HasColumnName("parameters");
        builder.Property(pending => pending.CreatedAt).HasColumnName("created_at");
        builder.Property(pending => pending.ExpiresAt).HasColumnName("expires_at");

        builder.HasIndex(pending => pending.ExpiresAt)
            .HasDatabaseName("ix_pending_authorizations_expires_at");

        builder.HasIndex(pending => pending.PreAuthentication)
            .HasDatabaseName("ux_pending_authorizations_preauthentication")
            .IsUnique()
            .HasFilter("preauthentication IS NOT NULL");

        builder.HasIndex(pending => pending.Session)
            .HasDatabaseName("ux_pending_authorizations_session")
            .IsUnique()
            .HasFilter("session IS NOT NULL");

        builder.HasOne<PreAuthenticationRecord>()
            .WithMany()
            .HasForeignKey(pending => pending.PreAuthentication)
            .HasConstraintName("fk_pending_authorizations_preauthentication")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SessionRecord>()
            .WithMany()
            .HasForeignKey(pending => pending.Session)
            .HasConstraintName("fk_pending_authorizations_session")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
