using System.Threading.Tasks;
using Xunit;

namespace Janus.Analyzers.Tests;

/// <summary>
/// JAN0004: blocking on a task, and an asynchronous method that takes no cancellation
/// token (CONV-CODE-008, CONV-CODE-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class BlockingAndCancellationAnalyzerTests
{
    /// <summary>
    /// CONV-CODE-008 AC1: reading a task's result is reported.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnBlockingOnATaskAsync()
    {
        string[] reported = await Analysis.OfAsync<BlockingAndCancellationAnalyzer>("""
            using System.Threading.Tasks;

            namespace Cases;

            internal sealed class Work
            {
                internal static int Now()
                {
                    return Later().Result;
                }

                private static Task<int> Later() => Task.FromResult(1);
            }
            """);

        Assert.Equal(["JAN0004"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: an asynchronous method that takes no cancellation token is
    /// reported.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnAnAsynchronousMethodWithoutACancellationTokenAsync()
    {
        string[] reported = await Analysis.OfAsync<BlockingAndCancellationAnalyzer>("""
            using System.Threading.Tasks;

            namespace Cases;

            internal sealed class Work
            {
                internal static async Task<int> LaterAsync()
                {
                    await Task.Yield();
                    return 1;
                }
            }
            """);

        Assert.Equal(["JAN0004"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: awaiting with a cancellation token is left alone.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_SilentOnAnAwaitWithACancellationTokenAsync()
    {
        string[] reported = await Analysis.OfAsync<BlockingAndCancellationAnalyzer>("""
            using System.Threading;
            using System.Threading.Tasks;

            namespace Cases;

            internal sealed class Work
            {
                internal static async Task<int> NowAsync(CancellationToken cancellationToken)
                {
                    return await Later(cancellationToken).ConfigureAwait(false);
                }

                private static Task<int> Later(CancellationToken cancellationToken) => Task.FromResult(1);
            }
            """);

        Assert.Empty(reported);
    }
}
