using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Janus.Storage.Privacy.Documents;

/// <summary>
/// How a translation attached to a published version is stored.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-005 and PRIV-CONS-006. A version holds at most one text in
/// each language, so correcting a translation replaces the row rather than adding a
/// second reading of the same version.
/// </remarks>
internal sealed class DocumentTranslationConfiguration
    : IEntityTypeConfiguration<DocumentTranslationRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DocumentTranslationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("legal_document_translations", table =>
            table.HasCheckConstraint(
                "ck_legal_document_translations_language",
                "length(trim(language)) > 0"));

        builder.HasKey(translation => new
        {
            translation.Name,
            translation.Version,
            translation.Language,
        }).HasName("pk_legal_document_translations");

        builder.Property(translation => translation.Name).HasColumnName("document");
        builder.Property(translation => translation.Version).HasColumnName("version");
        builder.Property(translation => translation.Language).HasColumnName("language");
        builder.Property(translation => translation.TranslatedText).HasColumnName("translated_text");

        builder.HasOne<DocumentVersionRecord>()
            .WithMany(version => version.Translations)
            .HasForeignKey(translation => new { translation.Name, translation.Version })
            .HasConstraintName("fk_legal_document_translations_version")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
