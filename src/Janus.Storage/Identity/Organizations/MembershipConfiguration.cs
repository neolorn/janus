using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Organizations;

/// <summary>
/// How a membership is stored.
/// </summary>
/// <remarks>
/// Implements IDN-MEM-001, IDN-MEM-002 and CONV-DESIGN-003. The account and the
/// organization each carry many rows, which is what lets
/// <c>organization.multiplememberships</c> be a setting rather than a migration.
/// </remarks>
internal sealed class MembershipConfiguration : IEntityTypeConfiguration<MembershipRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MembershipRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("memberships", table =>
        {
            // IDN-MEM-001: a membership that has ended ended after it began.
            table.HasCheckConstraint(
                "ck_memberships_ended",
                "ended_at IS NULL OR ended_at >= created_at");

            // REG-INV-001 AC3: an acknowledgement is its documents and its instant
            // together, or nothing.
            table.HasCheckConstraint(
                "ck_memberships_acknowledged",
                "(acknowledged_at IS NULL) = (acknowledged_documents IS NULL)");

            // IDN-MEM-001 (D-213): an end set in advance is its instant, its administrator and
            // its reason together, or nothing.
            table.HasCheckConstraint(
                "ck_memberships_end_set",
                """
                (ends_at IS NULL AND end_set_by IS NULL AND end_reason IS NULL)
                OR (ends_at IS NOT NULL AND end_set_by IS NOT NULL
                    AND length(btrim(end_reason)) BETWEEN 1 AND 1024)
                """);

            table.HasCheckConstraint(
                "ck_memberships_subject_not_max_uuid",
                MaxUuid.Refused("subject"));
            table.HasCheckConstraint(
                "ck_memberships_end_set_by_not_max_uuid",
                MaxUuid.Refused("end_set_by"));
        });

        builder.HasKey(membership => membership.Id).HasName("pk_memberships");

        builder.Property(membership => membership.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new MembershipId(value));

        builder.Property(membership => membership.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(membership => membership.Organization)
            .HasColumnName("organization")
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(membership => membership.CreatedAt).HasColumnName("created_at");
        builder.Property(membership => membership.EndedAt).HasColumnName("ended_at");

        builder.Property(membership => membership.AcknowledgedDocuments)
            .HasColumnName("acknowledged_documents")
            .HasColumnType("jsonb");

        builder.Property(membership => membership.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(membership => membership.EndsAt).HasColumnName("ends_at");

        builder.Property(membership => membership.EndSetBy)
            .HasColumnName("end_set_by")
            .HasConversion(subject => subject!.Value.Value, value => new SubjectId(value));

        builder.Property(membership => membership.EndReason).HasColumnName("end_reason");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(membership => membership.Subject)
            .HasConstraintName("fk_memberships_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(membership => membership.Organization)
            .HasConstraintName("fk_memberships_organization")
            .OnDelete(DeleteBehavior.Restrict);

        // An account's memberships are read together, and so are an organization's.
        builder.HasIndex(membership => membership.Subject)
            .HasDatabaseName("ix_memberships_subject");

        builder.HasIndex(membership => membership.Organization)
            .HasDatabaseName("ix_memberships_organization");

        // IDN-MEM-002, X3 of D-166: an account holds one current membership of an
        // organization, whatever commits together.
        builder.HasIndex(membership => new { membership.Subject, membership.Organization })
            .IsUnique()
            .HasDatabaseName("ux_memberships_current")
            .HasFilter("ended_at IS NULL");

    }
}
