using System.Threading.Tasks;
using Xunit;

namespace Janus.Analyzers.Tests;

/// <summary>
/// JAN0002: a logging call whose argument is marked as never logged
/// (CONV-CODE-008, CONV-LOG-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class NeverLoggedValueAnalyzerTests
{
    /// <summary>
    /// CONV-CODE-008 AC1: logging a marked value is reported.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_ReportedOnAMarkedValuePassedToALoggingCallAsync()
    {
        string[] reported = await Analysis.OfAsync<NeverLoggedValueAnalyzer>("""
            using Janus.Core;
            using Microsoft.Extensions.Logging;

            namespace Cases;

            [NeverLogged]
            internal sealed class VerificationCode;

            internal sealed class Verification
            {
                internal static void Record(ILogger logger, VerificationCode code)
                {
                    logger.LogInformation("Verification {Code}", code);
                }
            }
            """);

        Assert.Equal(["JAN0002"], reported);
    }

    /// <summary>
    /// CONV-CODE-008 AC1: logging the subject identifier instead is left alone.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_CODE_008_AC1_SilentOnAnUnmarkedValuePassedToALoggingCallAsync()
    {
        string[] reported = await Analysis.OfAsync<NeverLoggedValueAnalyzer>("""
            using Janus.Core;
            using Microsoft.Extensions.Logging;

            namespace Cases;

            [NeverLogged]
            internal sealed class VerificationCode;

            internal sealed class Verification
            {
                internal static void Record(ILogger logger, string subject)
                {
                    logger.LogInformation("Verification {Subject}", subject);
                }
            }
            """);

        Assert.Empty(reported);
    }
}
