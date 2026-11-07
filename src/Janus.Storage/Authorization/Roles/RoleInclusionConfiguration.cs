using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// How a role's inclusion of another is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-006 and CONV-DESIGN-003 (D-278). Each side keeps a key to the
/// role it names, so neither role is removed while the inclusion stands; the removal is
/// refused first by the read that finds it (<c>authz.role.inuse</c>).
/// </remarks>
internal sealed class RoleInclusionConfiguration : IEntityTypeConfiguration<RoleInclusionRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RoleInclusionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role_inclusions");

        builder.HasKey(inclusion => new { inclusion.Role, inclusion.Included })
            .HasName("pk_role_inclusions");

        builder.Property(inclusion => inclusion.Role)
            .HasColumnName("role")
            .HasConversion(role => role.ToString(), value => RoleName.Parse(value));

        builder.Property(inclusion => inclusion.Included)
            .HasColumnName("included")
            .HasConversion(role => role.ToString(), value => RoleName.Parse(value));

        builder.Property(inclusion => inclusion.Enabled)
            .HasColumnName("enabled");

        builder.Property(inclusion => inclusion.LeftOutPermissions)
            .HasColumnName("left_out_permissions")
            .HasDefaultValueSql("'{}'");

        builder.Property(inclusion => inclusion.LeftOutHandsOut)
            .HasColumnName("left_out_hands_out")
            .HasDefaultValueSql("'{}'");

        builder.HasIndex(inclusion => inclusion.Included)
            .HasDatabaseName("ix_role_inclusions_included");

        builder.HasOne<RoleRecord>()
            .WithMany()
            .HasForeignKey(inclusion => inclusion.Role)
            .HasConstraintName("fk_role_inclusions_role")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RoleRecord>()
            .WithMany()
            .HasForeignKey(inclusion => inclusion.Included)
            .HasConstraintName("fk_role_inclusions_included")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
