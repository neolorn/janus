using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Janus.Analyzers.Tests;

/// <summary>
/// JAN0003: a public or internal non-abstract class that is not sealed
/// (CONV-CODE-008, CONV-CODE-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class UnsealedTypeAnalyzerTests
{
    /// <summary>
    /// CONV-CODE-008 AC1: an unsealed class is reported.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnAnUnsealedClassAsync()
    {
        string[] reported = await Analysis.OfAsync<UnsealedTypeAnalyzer>("""
            namespace Cases;

            internal class Account
            {
            }
            """);

        Assert.Equal(["JAN0003"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: a sealed class and an abstract base are left alone, the
    /// second because it exists to be derived from.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_SilentOnASealedClassAndAnAbstractBaseAsync()
    {
        string[] reported = await Analysis.OfAsync<UnsealedTypeAnalyzer>("""
            namespace Cases;

            internal abstract class Principal
            {
            }

            internal sealed class Account : Principal
            {
            }
            """);

        Assert.Empty(reported);
    }

    /// <summary>
    /// CONV-CODE-001 AC1: an unsealed class that is not abstract is reported as an
    /// error, and the repository's .editorconfig does not lower it in any file the build
    /// analyses.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_001_AC1_AnUnsealedClassIsAnErrorInTheBuildAsync()
    {
        (string, ReportDiagnostic)[] reported = await Analysis.AsBuiltAsync<UnsealedTypeAnalyzer>("""
            namespace Cases;

            internal abstract class Principal
            {
            }

            internal class Account : Principal
            {
            }
            """);

        Assert.Equal([("JAN0003", ReportDiagnostic.Error)], reported);
    }
}
