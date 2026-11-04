namespace Janus.Authentication.Search;

/// <summary>
/// Why a process answers no staff search, the <c>details.reason</c> of
/// <c>identity.search.unavailable</c>.
/// </summary>
/// <remarks>Implements IDN-SRCH-003 and chapter 10 section 5.53 (D-256).</remarks>
internal enum SearchUnavailability
{
    /// <summary>
    /// The process's registration does not declare the search.
    /// </summary>
    Off = 0,

    /// <summary>
    /// The index is not built yet.
    /// </summary>
    Building = 1,

    /// <summary>
    /// The subjects to index pass <c>search.maxsubjects</c>.
    /// </summary>
    Capacity = 2,

    /// <summary>
    /// A memory check failed.
    /// </summary>
    Unsafe = 3,
}
