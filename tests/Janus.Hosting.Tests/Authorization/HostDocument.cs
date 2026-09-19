namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// One of the host's own records, which the library knows only by the identifier the
/// host registered it under.
/// </summary>
public sealed class HostDocument
{
    /// <summary>
    /// The host's own identifier, which is what the ancestry closure names.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// A field of the host's that the library neither reads nor knows about.
    /// </summary>
    public required string Title { get; init; }
}
