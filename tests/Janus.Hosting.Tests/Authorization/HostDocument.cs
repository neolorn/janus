using Janus.Core;

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

    /// <summary>
    /// The column naming the subject of the document's encrypted field, which is where
    /// the host reads the data subject it registers the record under (PRIV-RIGHT-005a).
    /// </summary>
    public SubjectId? Owner { get; init; }

    /// <summary>
    /// A field held under the owner's key.
    /// </summary>
    public string Notes { get; init; } = string.Empty;
}
