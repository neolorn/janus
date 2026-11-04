namespace Janus.Authentication.Search;

/// <summary>
/// One of the checks of what keeps the staff search's index off disk, named in the
/// <c>details.check</c> of <c>search-unsafe</c>.
/// </summary>
/// <remarks>Implements IDN-SRCH-003 and chapter 10 section 5.53 (D-256).</remarks>
internal enum MemoryCheck
{
    /// <summary>
    /// The cgroup's swap limit, <c>memory.swap.max</c>, is not 0.
    /// </summary>
    Swap = 0,

    /// <summary>
    /// The core-dump limit is not 0.
    /// </summary>
    CoreDump = 1,

    /// <summary>
    /// .NET's diagnostics port is on.
    /// </summary>
    Diagnostics = 2,

    /// <summary>
    /// Crash dumps are on.
    /// </summary>
    MiniDump = 3,
}
