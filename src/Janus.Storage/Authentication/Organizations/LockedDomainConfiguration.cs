using System;
using Janus.Core;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Organizations;

/// <summary>
/// How a domain in an organization's lock is stored.
/// </summary>
/// <remarks>
/// Implements REG-DOM-001, IDN-ORG-006, OPS-DB-001 and INF-DB-001. One row stands
/// listed per organization and domain, which the partial unique index holds; removed
/// rows sit beside it, so the history of a domain listed, removed and listed again is
/// every token it was drawn.
/// </remarks>
internal sealed class LockedDomainConfiguration : IEntityTypeConfiguration<LockedDomainRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "organization_domains";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<LockedDomainRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table => table.HasCheckConstraint(
            "ck_organization_domains_verified",
            "verified_at IS NULL OR checked_at IS NOT NULL"));

        builder.HasKey(domain => domain.Token).HasName("pk_organization_domains");

        builder.Property(domain => domain.Token).HasColumnName("token");

        builder.Property(domain => domain.Organization)
            .HasColumnName("organization")
            .HasConversion(organization => organization.Value, value => new OrganizationId(value));

        // OPS-DB-001 and INF-DB-001 AC3: the second plaintext column a person spells, so
        // the unique index compares it without regard to case as the name's does.
        builder.Property(domain => domain.Domain)
            .HasColumnName("domain")
            .UseCollation(StoreContext.CaseInsensitiveCollation);

        builder.Property(domain => domain.AddedAt).HasColumnName("added_at");
        builder.Property(domain => domain.VerifiedAt).HasColumnName("verified_at");
        builder.Property(domain => domain.CheckedAt).HasColumnName("checked_at");
        builder.Property(domain => domain.LastCheckPassed).HasColumnName("last_check_passed");
        builder.Property(domain => domain.RemovedAt).HasColumnName("removed_at");

        builder.HasIndex(domain => new { domain.Organization, domain.Domain })
            .HasDatabaseName("ux_organization_domains_organization_domain")
            .IsUnique()
            .HasFilter("removed_at IS NULL");

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(domain => domain.Organization)
            .HasConstraintName("fk_organization_domains_organization")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
