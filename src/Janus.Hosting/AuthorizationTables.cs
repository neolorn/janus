using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Hosting;

/// <summary>
/// Maps the two tables a permission filter reads into the host's own context, so that
/// a filtered listing is one query against the host's own database.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-002, LIB-HOST-002 and CONV-LAYOUT-002. The library owns both
/// tables and every migration over them; the host only reads them, which is why they
/// are mapped as views and take part in no migration of the host's.
/// </remarks>
public static class AuthorizationTables
{
    /// <summary>
    /// Maps <see cref="AncestryEntry"/> and <see cref="EffectiveGrant"/> onto
    /// <c>janus.ancestry</c> and <c>janus.effective_grants</c>.
    /// </summary>
    /// <param name="builder">The host's model.</param>
    /// <returns>The same model, for chaining.</returns>
    /// <exception cref="ArgumentNullException">The model is absent.</exception>
    public static ModelBuilder MapAuthorizationTables(this ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Entity<AncestryEntry>(entry =>
        {
            entry.ToView("ancestry", "janus");
            entry.HasKey(
                row => new { row.ResourceType, row.ResourceId, row.AncestorType, row.AncestorId });
            entry.Property(row => row.ResourceType).HasColumnName("resource_type");
            entry.Property(row => row.ResourceId).HasColumnName("resource_id");
            entry.Property(row => row.AncestorType).HasColumnName("ancestor_type");
            entry.Property(row => row.AncestorId).HasColumnName("ancestor_id");
            entry.Property(row => row.Depth).HasColumnName("depth");
            entry.Property(row => row.Organization).HasColumnName("organization");
        });

        builder.Entity<EffectiveGrant>(grant =>
        {
            grant.ToView("effective_grants", "janus");
            grant.HasKey(row => new { row.GrantId, row.Permission });
            grant.Property(row => row.GrantId).HasColumnName("grant_id");
            grant.Property(row => row.SubjectType).HasColumnName("subject_type");
            grant.Property(row => row.SubjectId).HasColumnName("subject_id");
            grant.Property(row => row.Role).HasColumnName("role");
            grant.Property(row => row.Permission).HasColumnName("permission");
            grant.Property(row => row.ResourceType).HasColumnName("resource_type");
            grant.Property(row => row.ResourceId).HasColumnName("resource_id");
            grant.Property(row => row.Deny).HasColumnName("deny");
            grant.Property(row => row.Kind).HasColumnName("kind");
            grant.Property(row => row.Organization).HasColumnName("organization");
            grant.Property(row => row.ExpiresAt).HasColumnName("expires_at");
            grant.Property(row => row.RevokedAt).HasColumnName("revoked_at");
        });

        return builder;
    }
}
