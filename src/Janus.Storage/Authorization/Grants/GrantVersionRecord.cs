using Janus.Core;

namespace Janus.Storage.Authorization.Grants;

/// <summary>
/// The <c>grant_versions</c> row: how many times what an account may do has changed.
/// </summary>
/// <remarks>
/// Implements AUTHZ-CACHE-001 and CONV-DESIGN-003. The number goes into the cache key
/// for the account's grant rows and group set, and is raised in the same transaction as
/// the change, so a bump orphans the previous entry and a revocation takes effect on the
/// next request with nothing to wait for.
/// </remarks>
internal sealed class GrantVersionRecord
{
    /// <summary>
    /// The <c>subject</c> column, which is this table's key.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>
    /// The <c>version</c> column.
    /// </summary>
    public long Version { get; set; }
}
