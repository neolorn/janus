using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// Which way the removal of access being made came: through the library's own routes or
/// through the host's own call.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281). The services' public contracts take no path, so the
/// route a request is served by says it: the boundary answers <c>administration</c> for a
/// request one of the library's routes serves and <c>library</c> for any other call.
/// </remarks>
internal interface IRemovalPaths
{
    /// <summary>
    /// The way the change being made came.
    /// </summary>
    AccessRemovalPath Current { get; }
}
