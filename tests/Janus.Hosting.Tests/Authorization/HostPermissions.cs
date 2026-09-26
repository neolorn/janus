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

    /// <summary>
    /// Publishing one of the host's records, which the host binds to a step-up gate.
    /// </summary>
    public static Permission Publish { get; } = Permission.Parse("document:publish");

    /// <summary>
    /// Recommending from one of the host's records, which the host binds to a purpose
    /// resting on consent.
    /// </summary>
    public static Permission Recommend { get; } = Permission.Parse("document:recommend");

    /// <summary>
    /// Exporting the host's records, which the host declares as an export operation
    /// (OPS-ALERT-006).
    /// </summary>
    public static Permission Export { get; } = Permission.Parse("document:export");

    /// <summary>
    /// Reading one of the host's records as part of the books the law has it keep,
    /// which the host binds to a purpose resting on a legal obligation.
    /// </summary>
    public static Permission Retain { get; } = Permission.Parse("document:retain");

    /// <summary>
    /// Reading one of the host's records of the type that discloses.
    /// </summary>
    public static Permission ReadNote { get; } = Permission.Parse("note:read");
}
