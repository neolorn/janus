using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Documents;

/// <summary>
/// How a published version of a legal document is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-005 and PRIV-CONS-006. The governing language and the
/// governing text are both required columns, so a version that binds in nothing
/// cannot reach the table by any path.
/// </remarks>
internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersionRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DocumentVersionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("legal_document_versions", table =>
        {
            table.HasCheckConstraint(
                "ck_legal_document_versions_governing_language",
                "length(trim(governing_language)) > 0");
            table.HasCheckConstraint(
                "ck_legal_document_versions_governing_text",
                "length(trim(governing_text)) > 0");
        });

        builder.HasKey(version => new { version.Name, version.Version })
            .HasName("pk_legal_document_versions");

        builder.Property(version => version.Name).HasColumnName("document");
        builder.Property(version => version.Version).HasColumnName("version");
        builder.Property(version => version.GoverningLanguage).HasColumnName("governing_language");
        builder.Property(version => version.GoverningText).HasColumnName("governing_text");
        builder.Property(version => version.PublishedAt).HasColumnName("published_at");

        // PRIV-CONS-005 AC1: without a version named, the current one is the last
        // published, which is one indexed read per document.
        builder.HasIndex(version => new { version.Name, version.PublishedAt })
            .HasDatabaseName("ix_legal_document_versions_current");
    }
}
