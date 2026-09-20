using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Documents;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Privacy.Documents;

/// <summary>
/// The published versions, over the <c>legal_document_versions</c> and
/// <c>legal_document_translations</c> tables.
/// </summary>
/// <param name="context">The context the operation's reads and writes run on.</param>
/// <remarks>
/// Implements PRIV-CONS-005, PRIV-CONS-006 and CONV-DESIGN-003. Every read carries
/// the translations with the governing text, because a screen that shows one has to
/// be able to show the other without asking again.
/// </remarks>
internal sealed class LegalDocumentStore(JanusDbContext context) : ILegalDocumentStore
{
    /// <inheritdoc/>
    public async ValueTask<DocumentVersion?> FindAsync(
        string document,
        string version,
        CancellationToken cancellationToken)
    {
        DocumentVersionRecord? record = await Reading()
            .FirstOrDefaultAsync(
                held => held.Name == document && held.Version == version,
                cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<DocumentVersion?> CurrentAsync(
        string document,
        CancellationToken cancellationToken)
    {
        DocumentVersionRecord? record = await Reading()
            .Where(held => held.Name == document)
            .OrderByDescending(held => held.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : Read(record);
    }

    /// <inheritdoc/>
    public async ValueTask<int> CountAsync(string document, CancellationToken cancellationToken) =>
        await context.LegalDocumentVersions
            .CountAsync(held => held.Name == document, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public ValueTask AddAsync(DocumentVersion version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);

        var record = new DocumentVersionRecord
        {
            Name = version.DocumentName,
            Version = version.Version,
            GoverningLanguage = version.GoverningLanguage,
            GoverningText = version.Text,
            PublishedAt = version.PublishedAt,
        };

        foreach (DocumentTranslation translation in version.Translations)
        {
            record.Translations.Add(new DocumentTranslationRecord
            {
                Name = version.DocumentName,
                Version = version.Version,
                Language = translation.Language,
                TranslatedText = translation.Text,
            });
        }

        context.LegalDocumentVersions.Add(record);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask TranslateAsync(
        string document,
        string version,
        DocumentTranslation translation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(translation);

        DocumentTranslationRecord? held = await context.LegalDocumentTranslations
            .FirstOrDefaultAsync(
                attached => attached.Name == document
                    && attached.Version == version
                    && attached.Language == translation.Language,
                cancellationToken)
            .ConfigureAwait(false);

        if (held is not null)
        {
            held.TranslatedText = translation.Text;

            return;
        }

        _ = await context.LegalDocumentVersions
                .FirstOrDefaultAsync(
                    record => record.Name == document && record.Version == version,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "The version " + version + " of " + document + " was never published.");

        context.LegalDocumentTranslations.Add(new DocumentTranslationRecord
        {
            Name = document,
            Version = version,
            Language = translation.Language,
            TranslatedText = translation.Text,
        });
    }

    private static DocumentVersion Read(DocumentVersionRecord record) =>
        new(
            record.Name,
            record.Version,
            record.GoverningLanguage,
            record.GoverningText,
            [
                .. record.Translations
                    .OrderBy(translation => translation.Language, StringComparer.Ordinal)
                    .Select(translation => new DocumentTranslation(
                        translation.Language,
                        translation.TranslatedText)),
            ],
            record.PublishedAt);

    private IQueryable<DocumentVersionRecord> Reading() =>
        context.LegalDocumentVersions.Include(version => version.Translations);
}
