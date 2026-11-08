using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// Maps the host's actions declared not inherited.
/// </summary>
/// <remarks>Implements AUTHZ-GRANT-006 (D-278).</remarks>
internal sealed class UninheritedPermissionConfiguration : IEntityTypeConfiguration<UninheritedPermissionRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UninheritedPermissionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("uninherited_permissions");

        builder.HasKey(row => row.Permission).HasName("pk_uninherited_permissions");

        builder.Property(row => row.Permission)
            .HasColumnName("permission")
            .HasConversion(permission => permission.ToString(), value => Permission.Parse(value));
    }
}
