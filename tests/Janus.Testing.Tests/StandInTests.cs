using System;
using System.IO;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Testing.Tests;

/// <summary>
/// The stand-ins for the optional environment seams, each answering a reading that
/// raises nothing (LIB-TEST-003, INT-GEN-006, AUTH-ABUSE-008).
/// </summary>
[Trait("kind", "unit")]
public sealed class StandInTests
{
    private static readonly DateTimeOffset Read = new(2026, 10, 20, 23, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// LIB-TEST-003: the location file and the datacenter range file each hold their
    /// first line alone, dated the day they are read, and no range.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_003_TheRangeFilesHoldOnlyTheirDateOfTheDayReadAsync()
    {
        var clock = new FixedClock(Read);

        string location = await TextAsync(await new EmptyLocationSource(clock).OpenAsync(TestContext.Current.CancellationToken));
        string ranges = await TextAsync(await new EmptyDatacenterRangeSource(clock).OpenAsync(TestContext.Current.CancellationToken));

        Assert.Equal("# 2026-10-20\n", location);
        Assert.Equal("# 2026-10-20\n", ranges);
    }

    private static async Task<string> TextAsync(Result<Stream> opened)
    {
        await using Stream file = opened.Match(stream => stream, error => throw new InvalidOperationException(error.Code.ToString()));
        using var reader = new StreamReader(file);

        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
