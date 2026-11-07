using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Roles;

/// <summary>
/// Maps the declared inclusions a start has seeded.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-006 (D-278). No key reaches the <c>roles</c> table, since the
/// pair stays seeded after the inclusion, or a role it named, is removed.
/// </remarks>
internal sealed class SeededRoleInclusionConfiguration : IEntityTypeConfiguration<SeededRoleInclusionRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SeededRoleInclusionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("seeded_role_inclusions");

        builder.HasKey(inclusion => new { inclusion.Role, inclusion.Included })
            .HasName("pk_seeded_role_inclusions");

        builder.Property(inclusion => inclusion.Role)
            .HasColumnName("role")
            .HasConversion(role => role.ToString(), value => RoleName.Parse(value));

        builder.Property(inclusion => inclusion.Included)
            .HasColumnName("included")
            .HasConversion(role => role.ToString(), value => RoleName.Parse(value));
    }
}
