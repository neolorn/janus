namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The container the host's records sit in, which it holds in a table of its own and
/// the library knows only by the identifier it was registered under.
/// </summary>
public sealed class HostWorkspace
{
    /// <summary>
    /// The host's own identifier.
    /// </summary>
    public required string Id { get; init; }
}
