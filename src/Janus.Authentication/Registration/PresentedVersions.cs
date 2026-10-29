using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Registration;

/// <summary>
/// The two legal documents the library names, and whether the version of one a screen
/// presented is its current version.
/// </summary>
/// <remarks>
/// Implements REG-SESS-007, REC-COL-002 and chapter 10 section 5.52. A person is recorded
/// as accepting or being shown only a text the deployment published and still stands
/// behind, so a version that is not the current one is refused, and a document with no
/// version published refuses every version.
/// </remarks>
internal static class PresentedVersions
{
    /// <summary>
    /// The privacy notice (PRIV-CONS-008a).
    /// </summary>
    public static DocumentName Notice { get; } = DocumentName.Parse("privacy-notice");

    /// <summary>
    /// The terms of service.
    /// </summary>
    public static DocumentName Terms { get; } = DocumentName.Parse("terms");

    /// <summary>
    /// Whether a version is the current version of its document.
    /// </summary>
    /// <param name="documents">The deployment's legal documents.</param>
    /// <param name="document">Which document.</param>
    /// <param name="version">The version the screen presented.</param>
    /// <param name="member">The member of the request that carried the version.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Nothing where it is, or <c>privacy.consent.superseded</c> naming the member,
    /// <c>privacy.notice.unpublished</c> naming the document, or the failure of the read.
    /// </returns>
    /// <exception cref="ArgumentNullException">A part is absent.</exception>
    public static async ValueTask<Error?> RefusedAsync(
        ILegalDocuments documents,
        DocumentName document,
        string version,
        string member,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(member);

        Result<DocumentVersion> current = await documents
            .ReadAsync(document, version: null, cancellationToken)
            .ConfigureAwait(false);

        return current.Match<Error?>(
            read => string.Equals(read.Version, version, StringComparison.Ordinal)
                ? null
                : Error.From(ErrorCodes.ConsentSuperseded, "member", JsonSerializer.SerializeToElement(member)),
            error => error.Code == ErrorCodes.DocumentNotFound
                ? Error.From(ErrorCodes.NoticeUnpublished, "document", JsonSerializer.SerializeToElement(document.ToString()))
                : error);
    }
}
