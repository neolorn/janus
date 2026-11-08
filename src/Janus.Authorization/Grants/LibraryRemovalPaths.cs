using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// The way of a removal made where no route of the library serves a request: the host's
/// own call.
/// </summary>
/// <remarks>Implements AUTHZ-GRANT-005 (D-281).</remarks>
internal sealed class LibraryRemovalPaths : IRemovalPaths
{
    /// <inheritdoc/>
    public AccessRemovalPath Current => AccessRemovalPath.Library;
}
