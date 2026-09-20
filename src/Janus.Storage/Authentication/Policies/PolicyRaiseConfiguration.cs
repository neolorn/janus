using System;
using Janus.Core;
using Janus.Storage.Identity.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Policies;

/// <summary>
/// How a raised requirement is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-017 and CONV-ENUM-001. One raise stands per scope per field,
/// which two partial unique indexes hold: the database treats absent organizations as
/// distinct from one another, so the deployment's own scope needs an index of its own
/// to be held to one row.
/// </remarks>
internal sealed class PolicyRaiseConfiguration : IEntityTypeConfiguration<PolicyRaiseRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "policy_raises";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PolicyRaiseRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table => table.HasCheckConstraint(
            "ck_policy_raises_field",
            Vocabulary.Admits<PolicyField>("field")));

        builder.HasKey(raise => raise.Id).HasName("pk_policy_raises");

        builder.Property(raise => raise.Id).HasColumnName("id");

        builder.Property(raise => raise.Organization)
            .HasColumnName("organization")
            .HasConversion(organization => organization!.Value.Value, value => new OrganizationId(value));

        builder.Property(raise => raise.Field)
            .HasColumnName("field")
            .HasConversion(new VocabularyConverter<PolicyField>());

        builder.Property(raise => raise.Value).HasColumnName("value");
        builder.Property(raise => raise.RaisedAt).HasColumnName("raised_at");

        builder.HasIndex(raise => new { raise.Organization, raise.Field })
            .HasDatabaseName("ux_policy_raises_organization_field")
            .IsUnique()
            .HasFilter("organization IS NOT NULL");

        builder.HasIndex(raise => raise.Field)
            .HasDatabaseName("ux_policy_raises_field")
            .IsUnique()
            .HasFilter("organization IS NULL");

        builder.HasOne<OrganizationRecord>()
            .WithMany()
            .HasForeignKey(raise => raise.Organization)
            .HasConstraintName("fk_policy_raises_organization")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
