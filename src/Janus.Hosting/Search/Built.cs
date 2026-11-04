namespace Janus.Hosting.Search;

/// <summary>
/// What a build of the staff search's index came to.
/// </summary>
/// <remarks>Implements IDN-SRCH-003 (D-256).</remarks>
internal enum Built
{
    /// <summary>
    /// The index was built and swapped in.
    /// </summary>
    Swapped = 0,

    /// <summary>
    /// The subjects to index pass <c>search.maxsubjects</c>, so nothing is built until the
    /// next start.
    /// </summary>
    Over = 1,

    /// <summary>
    /// A fault stopped the build, which is tried again.
    /// </summary>
    Failed = 2,
}
