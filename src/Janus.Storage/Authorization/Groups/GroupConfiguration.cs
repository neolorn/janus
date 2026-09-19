using System;
using Janus.Core;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Groups;

/// <summary>
/// How a group is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GROUP-001, AUTHZ-SCOPE-001 and CONV-DESIGN-003. A group belongs to
/// one organization, which is what keeps a grant it holds inside that organization.
/// </remarks>
internal sealed class GroupConfiguration : IEntityTypeConfiguration<GroupRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GroupRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("groups");

        builder.HasKey(group => group.Id).HasName("pk_groups");

        builder.Property(group => group.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new GroupId(value));

        builder.Property(group => group.Organization)
            .HasColumnName("organization")
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(group => group.Name).HasColumnName("name");

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(group => group.Organization)
            .HasConstraintName("fk_groups_organization")
            .OnDelete(DeleteBehavior.Restrict);

        // An organization's groups are listed together.
        builder.HasIndex(group => group.Organization).HasDatabaseName("ix_groups_organization");
    }
}
