using System.Threading.Tasks;
using Xunit;

namespace Janus.Analyzers.Tests;

/// <summary>
/// JAN0005: an outcome whose value is discarded (CONV-CODE-008, CONV-DESIGN-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class DiscardedResultAnalyzerTests
{
    /// <summary>
    /// CONV-CODE-008 AC1: calling an operation and throwing its outcome away is
    /// reported, whether the call stands alone or is assigned to a discard.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnADiscardedOutcomeAsync()
    {
        string[] reported = await Analysis.OfAsync<DiscardedResultAnalyzer>("""
            using Janus.Core;

            namespace Cases;

            internal sealed class Caller
            {
                internal static void Run()
                {
                    Store();
                    _ = Store();
                }

                private static Result Store() => Result.Success();
            }
            """);

        Assert.Equal(["JAN0005", "JAN0005"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: handling both cases is left alone.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_SilentOnAnOutcomeThatIsHandledAsync()
    {
        string[] reported = await Analysis.OfAsync<DiscardedResultAnalyzer>("""
            using Janus.Core;

            namespace Cases;

            internal sealed class Caller
            {
                internal static void Run()
                {
                    Store().Switch(() => { }, failure => { });
                }

                private static Result Store() => Result.Success();
            }
            """);

        Assert.Empty(reported);
    }
}
