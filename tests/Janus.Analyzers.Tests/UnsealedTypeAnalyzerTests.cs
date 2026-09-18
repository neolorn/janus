using System.Threading.Tasks;
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
}
