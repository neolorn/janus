namespace Janus.Authorization.Gate;

/// <summary>
/// One permission a page's record confers, and whether a deny took it away.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GATE-005. A page's capabilities are read in one query, so the
/// number of queries does not grow with the number of records on it.
/// </remarks>
internal sealed class PageCapability
{
    /// <summary>
    /// The record.
    /// </summary>
    public string Resource { get; init; } = string.Empty;

    /// <summary>
    /// The permission a grant named.
    /// </summary>
    public string Permission { get; init; } = string.Empty;

    /// <summary>
    /// Whether one of the grants naming it took it away.
    /// </summary>
    public bool Denied { get; init; }
}
