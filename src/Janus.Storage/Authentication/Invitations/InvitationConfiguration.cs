using System;
using Janus.Core;
using Janus.Storage.Authentication.Mailboxes;
using Janus.Storage.Identity.Accounts;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Invitations;

/// <summary>
/// How an invitation is stored.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-009a, REG-INV-001 and REG-MAIL-001. No foreign key names the
/// registration an invitation attached to: a registration that never completes leaves
/// nothing to point at. One invitation at most stands over a mailbox's reservation.
/// </remarks>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<InvitationRecord>
{
    /// <summary>The table, which the encrypted column names as its location.</summary>
    public const string Table = "invitations";

    /// <summary>The column what the invitation binds is held in.</summary>
    public const string IdentifiersColumn = "enc_identifiers";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InvitationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_invitations_key",
                "(enc_identifiers IS NULL) = (wrapped_key IS NULL) AND (wrapped_key IS NULL) = (key_version IS NULL)");
            table.HasCheckConstraint(
                "ck_invitations_forgotten",
                "enc_identifiers IS NULL OR (revoked_at IS NULL AND acknowledged_at IS NULL)");
            table.HasCheckConstraint(
                "ck_invitations_attached",
                "session IS NULL OR invitee IS NULL");
            table.HasCheckConstraint(
                "ck_invitations_outcome",
                "revoked_at IS NULL OR acknowledged_at IS NULL");
        });

        builder.HasKey(invitation => invitation.Id).HasName("pk_invitations");

        builder.Property(invitation => invitation.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new InvitationId(value));

        builder.Property(invitation => invitation.Organization)
            .HasColumnName("organization")
            .HasConversion(organization => organization.Value, value => new OrganizationId(value));

        builder.Property(invitation => invitation.Inviter)
            .HasColumnName("inviter")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(invitation => invitation.Token).HasColumnName("token");
        builder.Property(invitation => invitation.KeyVersion).HasColumnName("key_version");
        builder.Property(invitation => invitation.WrappedKey).HasColumnName("wrapped_key");
        builder.Property(invitation => invitation.EncryptedIdentifiers).HasColumnName(IdentifiersColumn);
        builder.Property(invitation => invitation.Roles).HasColumnName("roles");

        builder.Property(invitation => invitation.Documents)
            .HasColumnName("documents")
            .HasColumnType("jsonb");

        builder.Property(invitation => invitation.Mailbox).HasColumnName("mailbox");
        builder.Property(invitation => invitation.IssuedAt).HasColumnName("issued_at");
        builder.Property(invitation => invitation.ExpiresAt).HasColumnName("expires_at");

        builder.Property(invitation => invitation.Session)
            .HasColumnName("session")
            .HasConversion(session => session!.Value.Value, value => new RegistrationSessionId(value));

        builder.Property(invitation => invitation.Invitee)
            .HasColumnName("invitee")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(invitation => invitation.AttachedAt).HasColumnName("attached_at");
        builder.Property(invitation => invitation.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(invitation => invitation.RevokedAt).HasColumnName("revoked_at");

        // IDN-LIFE-009a AC2: the link is single use, so one token opens one invitation.
        builder.HasIndex(invitation => invitation.Token)
            .HasDatabaseName("ux_invitations_token")
            .IsUnique();

        // REG-MAIL-001: one invitation stands over a reservation, expired or not, until
        // it is revoked, replaced or acknowledged.
        builder.HasIndex(invitation => invitation.Mailbox)
            .HasDatabaseName("ux_invitations_mailbox")
            .IsUnique()
            .HasFilter("mailbox IS NOT NULL AND revoked_at IS NULL AND acknowledged_at IS NULL");

        builder.HasIndex(invitation => invitation.Organization)
            .HasDatabaseName("ix_invitations_organization");

        builder.HasIndex(invitation => invitation.Inviter)
            .HasDatabaseName("ix_invitations_inviter");

        builder.HasIndex(invitation => invitation.Invitee)
            .HasDatabaseName("ix_invitations_invitee");

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(invitation => invitation.Organization)
            .HasConstraintName("fk_invitations_organization")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(invitation => invitation.Inviter)
            .HasConstraintName("fk_invitations_inviter")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(invitation => invitation.Invitee)
            .HasConstraintName("fk_invitations_invitee")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MailboxRecord>()
            .WithMany()
            .HasForeignKey(invitation => invitation.Mailbox)
            .HasConstraintName("fk_invitations_mailbox")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
