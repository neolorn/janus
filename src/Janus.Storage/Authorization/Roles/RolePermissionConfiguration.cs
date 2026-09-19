using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// How a role's permissions are stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-CACHE-001 and CONV-DESIGN-003. The rows are read
/// through the <c>effective_grants</c> view wherever a grant naming the role is
/// evaluated, so editing a role takes effect at once and bumps nobody's counter.
/// </remarks>
internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RolePermissionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role_permissions");

        builder.HasKey(permission => new { permission.Role, permission.Permission })
            .HasName("pk_role_permissions");

        builder.Property(permission => permission.Role)
            .HasColumnName("role")
            .HasConversion(role => role.ToString(), value => RoleName.Parse(value));

        builder.Property(permission => permission.Permission)
            .HasColumnName("permission")
            .HasConversion(permission => permission.ToString(), value => Permission.Parse(value));

        builder.HasOne<RoleRecord>()
            .WithMany()
            .HasForeignKey(permission => permission.Role)
            .HasConstraintName("fk_role_permissions_role")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
