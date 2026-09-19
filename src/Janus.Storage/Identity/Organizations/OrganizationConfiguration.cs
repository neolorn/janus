using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Identity.Organizations;

/// <summary>
/// How an organization is stored.
/// </summary>
/// <remarks>
/// Implements IDN-ORG-001, IDN-ORG-002, IDN-ORG-003, IDN-ORG-004, OPS-DB-001 and
/// CONV-DESIGN-003.
/// The name is the first plaintext column a person spells, so it carries the
/// case-insensitive collation; no column says what kind of organization the row is.
/// </remarks>
internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<OrganizationRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OrganizationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("organizations", table =>
            // IDN-ORG-003: an erasure runs at the end of a window, so a row that
            // records one records the request that started it.
            table.HasCheckConstraint(
                "ck_organizations_erased",
                "erased_at IS NULL OR deletion_requested_at IS NOT NULL"));

        builder.HasKey(organization => organization.Id).HasName("pk_organizations");

        builder.Property(organization => organization.Id)
            .HasColumnName("id")
            .HasConversion(id => id.Value, value => new OrganizationId(value));

        builder.Property(organization => organization.Name)
            .HasColumnName("name")
            .UseCollation(JanusDbContext.CaseInsensitiveCollation);

        builder.Property(organization => organization.CreatedAt).HasColumnName("created_at");

        builder.Property(organization => organization.IsAdministrative)
            .HasColumnName("administrative")
            .HasDefaultValue(false);

        builder.Property(organization => organization.DeletionRequestedAt)
            .HasColumnName("deletion_requested_at");

        builder.Property(organization => organization.ErasedAt).HasColumnName("erased_at");

        // IDN-ORG-004: the mark is the domain's answer to which organization is the
        // administrative one, so the database holds it to exactly one row.
        builder.HasIndex(organization => organization.IsAdministrative)
            .HasDatabaseName("ux_organizations_administrative")
            .HasFilter("administrative")
            .IsUnique();

        // The sweep of OPS-OBS-003 reads the windows that have elapsed and nothing else.
        builder.HasIndex(organization => organization.DeletionRequestedAt)
            .HasDatabaseName("ix_organizations_deletion_requested_at")
            .HasFilter("deletion_requested_at IS NOT NULL AND erased_at IS NULL");
    }
}
