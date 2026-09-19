using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// How a role is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004 and CONV-DESIGN-003. The name is the key, because a grant
/// names the role and nothing else identifies it.
/// </remarks>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<RoleRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RoleRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("roles");

        builder.HasKey(role => role.Name).HasName("pk_roles");

        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasConversion(name => name.ToString(), value => RoleName.Parse(value));
    }
}
