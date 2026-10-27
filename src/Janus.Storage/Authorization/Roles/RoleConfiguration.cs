using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// How a role is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004, AUTHZ-GRANT-005 and CONV-DESIGN-003. The name is the key,
/// because a grant names the role and nothing else identifies it.
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

        // AUTHZ-GRANT-005 (D-212): a list on the row, as an invitation's roles are, with no
        // foreign key: a declared role may hand out one bootstrap has not seeded yet, and
        // the removal of a role a list names is refused by the read that finds it.
        builder.Property(role => role.HandsOut)
            .HasColumnName("hands_out")
            .HasDefaultValueSql("'{}'");
    }
}
