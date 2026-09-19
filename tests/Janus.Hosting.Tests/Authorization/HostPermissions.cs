using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The permissions the host declares for its own records. The library ships none of
/// these: what a host's roles may grant is the host's to name (AUTHZ-GRANT-004).
/// </summary>
internal static class HostPermissions
{
    /// <summary>
    /// Reading one of the host's records.
    /// </summary>
    public static Permission Read { get; } = Permission.Parse("document:read");

    /// <summary>
    /// Editing one of the host's records.
    /// </summary>
    public static Permission Edit { get; } = Permission.Parse("document:edit");
}
