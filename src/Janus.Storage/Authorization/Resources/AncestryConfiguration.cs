using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Resources;

/// <summary>
/// How the ancestry closure is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-INHERIT-002, AUTHZ-TEST-002 and LIB-API-001. The key is the
/// permission predicate's own lookup, record first; the second index is what a move and
/// a reverse lookup read. The table is public contract and a host maps it into its own
/// context to compose the filter (D-159). The organization is carried rather than
/// referenced: the resource row beside it holds the reference, written in the same
/// transaction, and an index over this column would serve no query while costing a
/// write on the largest table the library owns.
/// </remarks>
internal sealed class AncestryConfiguration : IEntityTypeConfiguration<AncestryRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AncestryRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ancestry", table =>
            table.HasCheckConstraint("ck_ancestry_depth", "depth >= 0"));

        builder.HasKey(entry => new
        {
            entry.Type,
            entry.Id,
            entry.AncestorType,
            entry.AncestorId,
        })
            .HasName("pk_ancestry");

        builder.Property(entry => entry.Type)
            .HasColumnName("resource_type")
            .HasConversion(type => type.ToString(), value => ResourceType.Parse(value));

        builder.Property(entry => entry.Id)
            .HasColumnName("resource_id")
            .HasConversion(id => id.ToString(), value => ResourceId.Parse(value));

        builder.Property(entry => entry.AncestorType)
            .HasColumnName("ancestor_type")
            .HasConversion(type => type.ToString(), value => ResourceType.Parse(value));

        builder.Property(entry => entry.AncestorId)
            .HasColumnName("ancestor_id")
            .HasConversion(id => id.ToString(), value => ResourceId.Parse(value));

        builder.Property(entry => entry.Depth).HasColumnName("depth");

        builder.Property(entry => entry.Organization)
            .HasColumnName("organization")
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        // A move rewrites the rows naming the moved record as an ancestor; the
        // administrative view reads the same way round.
        builder.HasIndex(entry => new { entry.AncestorType, entry.AncestorId })
            .HasDatabaseName("ix_ancestry_ancestor");
    }
}
