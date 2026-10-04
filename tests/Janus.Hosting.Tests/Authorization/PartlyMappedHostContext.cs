using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A host's context that holds its relationship's rows and mapped the ancestry and the
/// effective grants by hand, leaving out the consented resources, which no filter for
/// a permission bound to a consent-based purpose can run in.
/// </summary>
/// <param name="options">Where the context reaches its database.</param>
/// <remarks>
/// LIB-HOST-001, D-183: a relationship source naming such a context is refused at
/// startup.
/// </remarks>
internal sealed class PartlyMappedHostContext(DbContextOptions<PartlyMappedHostContext> options) : DbContext(options)
{
    /// <summary>
    /// The host's own record of who reviews what is in a workspace.
    /// </summary>
    public DbSet<HostReviewer> Reviewers => Set<HostReviewer>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<HostReviewer>(reviewer =>
        {
            reviewer.ToTable("reviewers", "host");
            reviewer.HasKey(row => new { row.WorkspaceId, row.Reviewer });
            reviewer.Property(row => row.WorkspaceId).HasColumnName("workspace_id");
            reviewer.Property(row => row.Reviewer)
                .HasColumnName("reviewer")
                .HasConversion(subject => subject.Value, value => new SubjectId(value));
        });

        modelBuilder.Entity<AncestryEntry>(entry =>
        {
            entry.ToView("ancestry", "identity");
            entry.HasKey(row => new { row.ResourceType, row.ResourceId, row.AncestorType, row.AncestorId });
        });

        modelBuilder.Entity<EffectiveGrant>(grant =>
        {
            grant.ToView("effective_grants", "identity");
            grant.HasKey(row => new { row.GrantId, row.Permission });
        });
    }
}
