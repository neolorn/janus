using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Documents;

namespace Janus.Privacy.Tests.Documents;

/// <summary>
/// The published versions, held in the order they were written so that the last one
/// is the current one.
/// </summary>
internal sealed class LegalDocumentStoreInMemory : ILegalDocumentStore
{
    private readonly List<DocumentVersion> _versions = [];

    /// <summary>
    /// Every version written, oldest first.
    /// </summary>
    public IReadOnlyList<DocumentVersion> Versions => _versions;

    /// <inheritdoc/>
    public ValueTask<DocumentVersion?> FindAsync(
        string document,
        string version,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_versions.LastOrDefault(held =>
            string.Equals(held.DocumentName, document, StringComparison.Ordinal)
            && string.Equals(held.Version, version, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public ValueTask<DocumentVersion?> CurrentAsync(
        string document,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_versions.LastOrDefault(held =>
            string.Equals(held.DocumentName, document, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public ValueTask<int> CountAsync(string document, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_versions.Count(held =>
            string.Equals(held.DocumentName, document, StringComparison.Ordinal)));

    /// <inheritdoc/>
    public ValueTask AddAsync(DocumentVersion version, CancellationToken cancellationToken)
    {
        _versions.Add(version);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask TranslateAsync(
        string document,
        string version,
        DocumentTranslation translation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(translation);

        int at = _versions.FindIndex(held =>
            string.Equals(held.DocumentName, document, StringComparison.Ordinal)
            && string.Equals(held.Version, version, StringComparison.Ordinal));

        if (at < 0)
        {
            throw new InvalidOperationException(
                "The version " + version + " of " + document + " was never published.");
        }

        DocumentVersion held = _versions[at];

        _versions[at] = held with
        {
            Translations =
            [
                .. held.Translations.Where(attached => !string.Equals(
                    attached.Language,
                    translation.Language,
                    StringComparison.Ordinal)),
                translation,
            ],
        };

        return ValueTask.CompletedTask;
    }
}
