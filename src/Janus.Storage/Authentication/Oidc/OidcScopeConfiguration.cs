using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Oidc;

/// <summary>
/// How a scope the deployment registered is stored.
/// </summary>
/// <remarks>
/// Implements AUTH-OIDC-001. The name is unique because a request asks for a scope by
/// it and two rows answering to one name would make which of them applies a matter of
/// ordering.
/// </remarks>
internal sealed class OidcScopeConfiguration : IEntityTypeConfiguration<OidcScopeRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "oidc_scopes";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<OidcScopeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table);

        builder.HasKey(scope => scope.Id).HasName("pk_oidc_scopes");

        builder.Property(scope => scope.Id).HasColumnName("id");
        builder.Property(scope => scope.Name).HasColumnName("name");
        builder.Property(scope => scope.DisplayName).HasColumnName("display_name");

        builder.Property(scope => scope.DisplayNames)
            .HasColumnName("display_names")
            .HasColumnType("jsonb");

        builder.Property(scope => scope.Description).HasColumnName("description");

        builder.Property(scope => scope.Descriptions)
            .HasColumnName("descriptions")
            .HasColumnType("jsonb");

        builder.Property(scope => scope.Resources).HasColumnName("resources");

        builder.Property(scope => scope.Properties)
            .HasColumnName("properties")
            .HasColumnType("jsonb");

        builder.HasIndex(scope => scope.Name).IsUnique().HasDatabaseName("ux_oidc_scopes_name");
    }
}
