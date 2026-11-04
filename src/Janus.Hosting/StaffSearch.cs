namespace Janus.Hosting;

/// <summary>
/// Whether the process serves the staff search, declared at registration as the
/// application's kind is.
/// </summary>
/// <remarks>
/// Implements IDN-SRCH-003 and LIB-HOST-001 (D-256). A host declares the search in the
/// processes that serve staff; a process that serves it holds the search's index in its
/// own memory.
/// </remarks>
public enum StaffSearch
{
    /// <summary>
    /// The process builds no index, and the search answers
    /// <c>identity.search.unavailable</c> with <c>details.reason</c> <c>off</c>.
    /// </summary>
    Off = 0,

    /// <summary>
    /// The process builds the index once the host has started, and serves the search.
    /// </summary>
    Served = 1,
}
