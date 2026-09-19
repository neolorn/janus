namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A record of the host's whose denial says the record exists and is forbidden, which
/// is the opt-in of AUTHZ-CONCEAL-001 and the only kind of type whose explanation is
/// self-service (AUTHZ-GATE-004).
/// </summary>
public sealed class HostNote
{
    /// <summary>
    /// The host's own identifier.
    /// </summary>
    public required string Id { get; init; }
}
