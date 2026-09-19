using System;
using Janus.Core;
using Janus.Storage.Identity.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// How an account's grant version is stored.
/// </summary>
/// <remarks>
/// Implements AUTHZ-CACHE-001 and CONV-DESIGN-003. One row per account, raised where a
/// grant or a membership changes, never read for anything but a cache key.
/// </remarks>
internal sealed class GrantVersionConfiguration : IEntityTypeConfiguration<GrantVersionRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<GrantVersionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("grant_versions", table =>
            table.HasCheckConstraint("ck_grant_versions_version", "version >= 0"));

        builder.HasKey(version => version.Subject).HasName("pk_grant_versions");

        builder.Property(version => version.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(version => version.Version).HasColumnName("version");

        builder.HasOne<AccountRecord>()
            .WithMany()
            .HasForeignKey(version => version.Subject)
            .HasConstraintName("fk_grant_versions_subject")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
