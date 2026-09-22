using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Documents;

/// <summary>
/// Where the published versions of the legal documents are.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-005, PRIV-CONS-006 and CONV-DESIGN-003. A version is written
/// once and never edited; a translation attaches to one and makes no new version.
/// </remarks>
internal interface ILegalDocumentStore
{
    /// <summary>
    /// One version of a document.
    /// </summary>
    /// <param name="document">Which document.</param>
    /// <param name="version">Which version.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The version, or nothing where it was never published.</returns>
    ValueTask<DocumentVersion?> FindAsync(
        string document,
        string version,
        CancellationToken cancellationToken);

    /// <summary>
    /// The current version of a document, which is the last published.
    /// </summary>
    /// <param name="document">Which document.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The version, or nothing where the deployment published none.</returns>
    ValueTask<DocumentVersion?> CurrentAsync(string document, CancellationToken cancellationToken);

    /// <summary>
    /// How many versions of a document are published, which is what the next one is
    /// numbered from.
    /// </summary>
    /// <param name="document">Which document.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The count, which is zero where the deployment published none.</returns>
    ValueTask<int> CountAsync(string document, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a new version.
    /// </summary>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of writing it.</returns>
    ValueTask AddAsync(DocumentVersion version, CancellationToken cancellationToken);

    /// <summary>
    /// Attaches a translation to a published version, or replaces the one it already
    /// carries in that language.
    /// </summary>
    /// <param name="document">Which document.</param>
    /// <param name="version">Which version.</param>
    /// <param name="translation">The translation.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of attaching it.</returns>
    /// <exception cref="System.InvalidOperationException">No such version exists.</exception>
    ValueTask TranslateAsync(
        string document,
        string version,
        DocumentTranslation translation,
        CancellationToken cancellationToken);
}
