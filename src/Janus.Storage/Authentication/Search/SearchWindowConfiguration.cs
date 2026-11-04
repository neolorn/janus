using System;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Authentication.Search;

/// <summary>
/// How each account's staff searches a minute are stored.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-002 (D-256). A row a caller and a minute, so the count is one
/// upsert, and the minutes before it are forgotten by the same statement.
/// </remarks>
internal sealed class SearchWindowConfiguration : IEntityTypeConfiguration<SearchWindowRecord>
{
    /// <summary>The table.</summary>
    public const string Table = "search_windows";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<SearchWindowRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(Table, table => table.HasCheckConstraint(
            "ck_search_windows_subject_not_max_uuid",
            MaxUuid.Refused("subject")));

        builder.HasKey(window => new { window.Subject, window.WindowStart }).HasName("pk_search_windows");

        builder.Property(window => window.Subject)
            .HasColumnName("subject")
            .HasConversion(subject => subject.Value, value => new SubjectId(value));

        builder.Property(window => window.WindowStart).HasColumnName("window_start");

        builder.Property(window => window.Count).HasColumnName("count");
    }
}
