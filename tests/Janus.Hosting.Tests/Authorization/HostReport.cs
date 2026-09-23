namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A record of the host's that no derivation reaches: it sits in no workspace and
/// declares none of its own, so the stored grants are the whole of what decides it.
/// </summary>
public sealed class HostReport
{
    /// <summary>
    /// The host's own identifier.
    /// </summary>
    public required string Id { get; init; }
}
