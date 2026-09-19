using System;
using Janus.Core;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// How a grant is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-001 to AUTHZ-GRANT-003, AUTHZ-TEST-002, CONV-ENUM-001 and
/// CONV-DESIGN-003. The partial index over the grants not revoked is what keeps the
/// nested existence check of the permission predicate cheap at production volume.
/// </remarks>
internal sealed class GrantConfiguration : IEntityTypeConfiguration<GrantRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GrantRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("grants", table =>
        {
            table.HasCheckConstraint(
                "ck_grants_subject_type",
                Vocabulary.Admits<SubjectType>("subject_type"));

            table.HasCheckConstraint("ck_grants_kind", Vocabulary.Admits<GrantKind>("kind"));

            // AUTHZ-GRANT-001 AC2: a grant names a record or it names none; half of one
            // is neither a record nor the organization.
            table.HasCheckConstraint(
                "ck_grants_resource",
                "(resource_type IS NULL) = (resource_id IS NULL)");

            // AUTHZ-GRANT-003, D-153: a grant states why, and so does its revocation.
            table.HasCheckConstraint(
                "ck_grants_reason",
                "length(btrim(reason)) BETWEEN 1 AND 1024");

            table.HasCheckConstraint(
                "ck_grants_revocation",
                """
                (revoked_at IS NULL AND revoked_by IS NULL AND revocation_reason IS NULL)
                OR (revoked_at IS NOT NULL AND revoked_by IS NOT NULL
                    AND length(btrim(revocation_reason)) BETWEEN 1 AND 1024)
                """);
        });

        builder.HasKey(grant => grant.Id).HasName("pk_grants");

        builder.Property(grant => grant.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new GrantId(value));

        builder.Property(grant => grant.SubjectType)
            .HasColumnName("subject_type")
            .HasConversion(new VocabularyConverter<SubjectType>());

        builder.Property(grant => grant.SubjectId).HasColumnName("subject_id");

        builder.Property(grant => grant.Role)
            .HasColumnName("role")
            .HasConversion(role => role.ToString(), value => RoleName.Parse(value));

        builder.Property(grant => grant.Organization)
            .HasColumnName("organization")
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(grant => grant.ResourceType)
            .HasColumnName("resource_type")
            .HasConversion(new ValueConverter<ResourceType, string>(
                type => type.ToString(),
                value => ResourceType.Parse(value)));

        builder.Property(grant => grant.ResourceId)
            .HasColumnName("resource_id")
            .HasConversion(new ValueConverter<ResourceId, string>(
                id => id.ToString(),
                value => ResourceId.Parse(value)));

        builder.Property(grant => grant.Deny).HasColumnName("deny");

        builder.Property(grant => grant.Kind)
            .HasColumnName("kind")
            .HasConversion(new VocabularyConverter<GrantKind>());

        builder.Property(grant => grant.ExpiresAt).HasColumnName("expires_at");

        builder.Property(grant => grant.GrantedBy)
            .HasColumnName("granted_by")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(grant => grant.GrantedAt).HasColumnName("granted_at");
        builder.Property(grant => grant.Reason).HasColumnName("reason");

        builder.Property(grant => grant.RevokedBy)
            .HasColumnName("revoked_by")
            .HasConversion(new ValueConverter<SubjectId, Guid>(
                subject => subject.Value,
                value => new SubjectId(value)));

        builder.Property(grant => grant.RevokedAt).HasColumnName("revoked_at");
        builder.Property(grant => grant.RevocationReason).HasColumnName("revocation_reason");

        builder.HasOne<RoleRecord>()
            .WithMany()
            .HasForeignKey(grant => grant.Role)
            .HasConstraintName("fk_grants_role")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(grant => grant.Organization)
            .HasConstraintName("fk_grants_organization")
            .OnDelete(DeleteBehavior.Restrict);

        // AUTHZ-TEST-002: the permission predicate reaches a grant by its holder inside
        // one organization, and never wants a revoked one.
        builder.HasIndex(grant => new
        {
            grant.Organization,
            grant.SubjectType,
            grant.SubjectId,
        })
            .HasDatabaseName("ix_grants_live_holder")
            .HasFilter("revoked_at IS NULL");

        // The grants naming a role are read when the role is edited or removed.
        builder.HasIndex(grant => grant.Role).HasDatabaseName("ix_grants_role");

        // AUTHZ-DERIVE-007, AUTHZ-GATE-004: who can reach this record.
        builder.HasIndex(grant => new { grant.ResourceType, grant.ResourceId })
            .HasDatabaseName("ix_grants_live_resource")
            .HasFilter("revoked_at IS NULL");
    }
}
