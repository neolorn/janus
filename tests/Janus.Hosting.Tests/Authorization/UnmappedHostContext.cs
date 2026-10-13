using System;
using Microsoft.EntityFrameworkCore;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A host's context that holds its relationship's rows and never mapped the library's
/// contract tables into itself, which no evaluation of a derivation can run in.
/// </summary>
/// <param name="options">Where the context reaches its database.</param>
/// <remarks>
/// LIB-HOST-001: a relationship source naming such a context is refused at startup.
/// </remarks>
internal sealed class UnmappedHostContext(DbContextOptions<UnmappedHostContext> options) : DbContext(options)
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
                .HasConversion(subject => subject.Value, value => new Janus.Core.SubjectId(value));
        });
    }
}
