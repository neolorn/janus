using System;

namespace Janus.Storage.Privacy.Documents;

/// <summary>
/// The <c>legal_document_cookies</c> row.
/// </summary>
/// <remarks>
/// Implements PRIV-CONS-006a (D-203). A row is written with its version and never
/// edited, so a reader of an old version of the notice sees the cookies it listed.
/// </remarks>
internal sealed class DocumentCookieRecord
{
    /// <summary>
    /// The <c>document</c> column.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The <c>version</c> column.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The <c>cookie</c> column: what the cookie is called.
    /// </summary>
    public string Cookie { get; set; } = string.Empty;

    /// <summary>
    /// The <c>purpose</c> column: the consent-based purpose it serves, or null where it
    /// is strictly necessary.
    /// </summary>
    public string? Purpose { get; set; }

    /// <summary>
    /// The <c>lifetime</c> column: how long it is set for, or null where it ends with
    /// the browser's session.
    /// </summary>
    public TimeSpan? Lifetime { get; set; }

    /// <summary>
    /// The <c>third_party</c> column: whether any third party can read it.
    /// </summary>
    public bool ThirdParty { get; set; }
}
