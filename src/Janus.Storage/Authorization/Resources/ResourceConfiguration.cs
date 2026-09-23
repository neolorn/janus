using System;
using Janus.Core;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Janus.Storage.Authorization.Resources;

/// <summary>
/// How a registered record is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-001, AUTHZ-SCOPE-001 and CONV-DESIGN-003. The container is
/// a record of this table too, so a move is one row changed and the ancestry rewritten
/// beneath it.
/// </remarks>
internal sealed class ResourceConfiguration : IEntityTypeConfiguration<ResourceRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ResourceRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("resources", table =>
            // A record is contained in something or in nothing; half a reference is
            // neither.
            table.HasCheckConstraint(
                "ck_resources_contained_in",
                "(contained_in_type IS NULL) = (contained_in_id IS NULL)"));

        builder.HasKey(resource => new { resource.Type, resource.Id }).HasName("pk_resources");

        builder.Property(resource => resource.Type)
            .HasColumnName("resource_type")
            .HasConversion(type => type.ToString(), value => ResourceType.Parse(value));

        builder.Property(resource => resource.Id)
            .HasColumnName("resource_id")
            .HasConversion(id => id.ToString(), value => ResourceId.Parse(value));

        builder.Property(resource => resource.Organization)
            .HasColumnName("organization")
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(resource => resource.Subject)
            .HasColumnName("subject")
            .HasConversion(new ValueConverter<SubjectId, Guid>(
                id => id.Value,
                value => new SubjectId(value)));

        builder.Property(resource => resource.ContainedInType)
            .HasColumnName("contained_in_type")
            .HasConversion(new ValueConverter<ResourceType, string>(
                type => type.ToString(),
                value => ResourceType.Parse(value)));

        builder.Property(resource => resource.ContainedInId)
            .HasColumnName("contained_in_id")
            .HasConversion(new ValueConverter<ResourceId, string>(
                id => id.ToString(),
                value => ResourceId.Parse(value)));

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(resource => resource.Organization)
            .HasConstraintName("fk_resources_organization")
            .OnDelete(DeleteBehavior.Restrict);

        // An organization's records are listed together.
        builder.HasIndex(resource => resource.Organization)
            .HasDatabaseName("ix_resources_organization");

        // A move rewrites everything the record contains, found by this index.
        builder.HasIndex(resource => new { resource.ContainedInType, resource.ContainedInId })
            .HasDatabaseName("ix_resources_contained_in");
    }
}
