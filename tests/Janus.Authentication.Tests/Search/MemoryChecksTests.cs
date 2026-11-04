using Janus.Authentication.Search;
using Xunit;

namespace Janus.Authentication.Tests.Search;

/// <summary>
/// What keeps the staff search's index off disk, checked before it is built
/// (IDN-SRCH-003, D-256).
/// </summary>
[Trait("kind", "unit")]
public sealed class MemoryChecksTests
{
    private const string NoCoreDumps =
        "Limit                     Soft Limit           Hard Limit           Units     \n"
        + "Max cpu time              unlimited            unlimited            seconds   \n"
        + "Max core file size        0                    0                    bytes     \n";

    private const string CoreDumps =
        "Limit                     Soft Limit           Hard Limit           Units     \n"
        + "Max core file size        unlimited            unlimited            bytes     \n";

    /// <summary>
    /// A process with no swap, no core dumps, the diagnostics port off and crash dumps
    /// off fails no check.
    /// </summary>
    [Fact]
    public void Failed_AProcessKeptOffDisk_FailsNothing() =>
        Assert.Empty(MemoryChecks.Failed("0\n", NoCoreDumps, "0", null));

    /// <summary>
    /// Swap allowed, core dumps on, the diagnostics port on or unset, and crash dumps on
    /// each fail their own check.
    /// </summary>
    [Fact]
    public void Failed_EachSourceOfADiskCopy_FailsItsCheck()
    {
        Assert.Equal([MemoryCheck.Swap], MemoryChecks.Failed("max\n", NoCoreDumps, "0", "0"));
        Assert.Equal([MemoryCheck.CoreDump], MemoryChecks.Failed("0", CoreDumps, "0", null));
        Assert.Equal([MemoryCheck.Diagnostics], MemoryChecks.Failed("0", NoCoreDumps, "1", null));
        Assert.Equal([MemoryCheck.Diagnostics], MemoryChecks.Failed("0", NoCoreDumps, null, null));
        Assert.Equal([MemoryCheck.MiniDump], MemoryChecks.Failed("0", NoCoreDumps, "0", "1"));
    }

    /// <summary>
    /// IDN-SRCH-003 AC10: where <c>memory.swap.max</c> or <c>/proc/self/limits</c> cannot be
    /// read, the check it serves counts as failed.
    /// </summary>
    [Fact]
    public void IDN_SRCH_003_AC10_ACheckWhoseSourceCannotBeReadFails() =>
        Assert.Equal([MemoryCheck.Swap, MemoryCheck.CoreDump], MemoryChecks.Failed(null, null, "0", null));
}
