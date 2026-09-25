using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Janus.Analyzers.Tests;

/// <summary>
/// JAN0001: a catch block that returns a permitted, authenticated or successful
/// outcome (CONV-CODE-008, CONV-ERR-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class PermittedOutcomeFromCatchAnalyzerTests
{
    /// <summary>
    /// CONV-CODE-008 AC1: a catch that permits is reported.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnAPermissionReturnedFromACatchAsync()
    {
        string[] reported = await Analysis.OfAsync<PermittedOutcomeFromCatchAnalyzer>("""
            namespace Cases;

            internal sealed class Gate
            {
                internal static bool Allow()
                {
                    try
                    {
                        return Decide();
                    }
                    catch (System.InvalidOperationException)
                    {
                        return true;
                    }
                }

                private static bool Decide() => false;
            }
            """);

        Assert.Equal(["JAN0001"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: a catch that returns a successful outcome is reported on the
    /// same ground as one that returns true.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnASuccessfulOutcomeReturnedFromACatchAsync()
    {
        string[] reported = await Analysis.OfAsync<PermittedOutcomeFromCatchAnalyzer>("""
            using Janus.Core;

            namespace Cases;

            internal sealed class Gate
            {
                internal static Result Allow()
                {
                    try
                    {
                        return Decide();
                    }
                    catch (System.InvalidOperationException)
                    {
                        return Result.Success();
                    }
                }

                private static Result Decide() => Result.Success();
            }
            """);

        Assert.Equal(["JAN0001"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: a catch that refuses is left alone.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_SilentOnARefusalReturnedFromACatchAsync()
    {
        string[] reported = await Analysis.OfAsync<PermittedOutcomeFromCatchAnalyzer>("""
            using Janus.Core;

            namespace Cases;

            internal sealed class Gate
            {
                internal static Result Allow()
                {
                    try
                    {
                        return Decide();
                    }
                    catch (System.InvalidOperationException)
                    {
                        return Result.Failure(Error.From(ErrorCodes.ConfigurationKeyProtected));
                    }
                }

                private static Result Decide() => Result.Success();
            }
            """);

        Assert.Empty(reported);
    }

    /// <summary>
    /// CONV-ERR-002 AC1: a catch that permits is reported as an error, and the
    /// repository's .editorconfig does not lower it in any file the build analyses.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_ERR_002_AC1_APermissionFromACatchIsAnErrorInTheBuildAsync()
    {
        (string, ReportDiagnostic)[] reported = await Analysis.AsBuiltAsync<PermittedOutcomeFromCatchAnalyzer>("""
            namespace Cases;

            internal sealed class Gate
            {
                internal static bool Allow()
                {
                    try
                    {
                        return Decide();
                    }
                    catch (System.InvalidOperationException)
                    {
                        return true;
                    }
                }

                private static bool Decide() => false;
            }
            """);

        Assert.Equal([("JAN0001", ReportDiagnostic.Error)], reported);
    }
}
