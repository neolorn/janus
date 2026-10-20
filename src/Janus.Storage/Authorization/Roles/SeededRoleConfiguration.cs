using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// Maps the names of the declared roles a start has seeded.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-004 (D-204). No key reaches the <c>roles</c> table, since the
/// name stays seeded after the role it named is removed.
/// </remarks>
internal sealed class SeededRoleConfiguration : IEntityTypeConfiguration<SeededRoleRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SeededRoleRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("seeded_roles");

        builder.HasKey(role => role.Name).HasName("pk_seeded_roles");

        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasConversion(name => name.ToString(), value => RoleName.Parse(value));
    }
}
