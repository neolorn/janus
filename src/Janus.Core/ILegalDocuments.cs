using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The legal documents a deployment publishes: the privacy notice, the terms of
/// service, the consent texts and anything else it holds, each version carrying the
/// one language it binds in.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, PRIV-CONS-005, PRIV-CONS-006, PRIV-CONS-007 and chapter 09
/// sections 7 and 8a. A version follows the governing text: correcting a translation
/// changes nothing about what was shown as authoritative, so it makes no new version.
/// </remarks>
public interface ILegalDocuments
{
    /// <summary>
    /// One version of a document, or the current one where none is named.
    /// </summary>
    /// <param name="document">Which document.</param>
    /// <param name="version">Which version, or nothing for the current one.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The version, or the refusal where the deployment published none.</returns>
    ValueTask<Result<DocumentVersion>> ReadAsync(
        string document,
        string? version,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes a new version. A publication carrying no governing-language text is
    /// refused with <c>privacy.notice.governingtextmissing</c> and the condition is
    /// raised.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="publication">What is being published.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The published version, or the refusal and its code.</returns>
    ValueTask<Result<DocumentVersion>> PublishAsync(
        AccessContext context,
        DocumentPublication publication,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attaches or corrects a translation on a published version, making no new
    /// version.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="document">Which document.</param>
    /// <param name="version">Which version.</param>
    /// <param name="translation">The translation.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, or the refusal and its code.</returns>
    ValueTask<Result> TranslateAsync(
        AccessContext context,
        string document,
        string version,
        DocumentTranslation translation,
        CancellationToken cancellationToken);
}
