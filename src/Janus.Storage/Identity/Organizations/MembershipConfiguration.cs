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
            // IDN-MEM-001: a membership that has ended ended after it began.
            table.HasCheckConstraint(
                "ck_memberships_ended",
                "ended_at IS NULL OR ended_at >= created_at"));

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
    }
}
