using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Janus.Analyzers.Tests;

/// <summary>
/// JAN0006: a catch block that is empty, or whose every path neither throws, rethrows,
/// returns a failure outcome nor logs (CONV-CODE-008, CONV-ERR-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class SwallowedExceptionAnalyzerTests
{
    /// <summary>
    /// CONV-CODE-008 AC1: an empty catch is reported.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnAnEmptyCatchAsync()
    {
        string[] reported = await Analysis.OfAsync<SwallowedExceptionAnalyzer>("""
            namespace Cases;

            internal sealed class Reader
            {
                internal static void Run()
                {
                    try
                    {
                        Touch();
                    }
                    catch (System.IO.IOException)
                    {
                    }
                }

                private static void Touch()
                {
                }
            }
            """);

        Assert.Equal(["JAN0006"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: a catch that carries on without dealing with the exception
    /// is reported although it is not empty.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnACatchThatOnlyCarriesOnAsync()
    {
        string[] reported = await Analysis.OfAsync<SwallowedExceptionAnalyzer>("""
            namespace Cases;

            internal sealed class Reader
            {
                internal static int Run()
                {
                    int count = 0;

                    try
                    {
                        Touch();
                    }
                    catch (System.IO.IOException)
                    {
                        count = 1;
                    }

                    return count;
                }

                private static void Touch()
                {
                }
            }
            """);

        Assert.Equal(["JAN0006"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: a catch that rethrows, one that returns a failure outcome
    /// and one that logs are all left alone.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_SilentOnACatchThatDealsWithTheExceptionAsync()
    {
        string[] reported = await Analysis.OfAsync<SwallowedExceptionAnalyzer>("""
            using Janus.Core;
            using Microsoft.Extensions.Logging;

            namespace Cases;

            internal sealed class Reader
            {
                internal static void Rethrow()
                {
                    try
                    {
                        Touch();
                    }
                    catch (System.IO.IOException)
                    {
                        throw;
                    }
                }

                internal static Result Refuse()
                {
                    try
                    {
                        Touch();
                        return Result.Success();
                    }
                    catch (System.IO.IOException)
                    {
                        return Result.Failure(Error.From(ErrorCodes.ConfigurationKeyProtected));
                    }
                }

                internal static void Report(ILogger logger)
                {
                    try
                    {
                        Touch();
                    }
                    catch (System.IO.IOException exception)
                    {
                        logger.LogWarning(exception, "The store could not be read.");
                    }
                }

                private static void Touch()
                {
                }
            }
            """);

        Assert.Empty(reported);
    }

    /// <summary>
    /// CONV-ERR-003 AC1: an empty catch and a catch that neither throws, rethrows,
    /// returns a failure nor logs are each reported as an error, and the repository's
    /// .editorconfig lowers neither in any file the build analyses.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_ERR_003_AC1_ASwallowedExceptionIsAnErrorInTheBuildAsync()
    {
        (string, ReportDiagnostic)[] reported = await Analysis.AsBuiltAsync<SwallowedExceptionAnalyzer>("""
            namespace Cases;

            internal sealed class Reader
            {
                internal static void Run()
                {
                    try
                    {
                        Touch();
                    }
                    catch (System.IO.IOException)
                    {
                    }
                }

                internal static int Count()
                {
                    int count = 0;

                    try
                    {
                        Touch();
                    }
                    catch (System.IO.IOException)
                    {
                        count = 1;
                    }

                    return count;
                }

                private static void Touch()
                {
                }
            }
            """);

        Assert.Equal([("JAN0006", ReportDiagnostic.Error), ("JAN0006", ReportDiagnostic.Error)], reported);
    }
}
