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
    /// CONV-LOG-003 AC1: the rule fires on the library's own carriers of a forbidden
    /// value, as they are marked in <c>Janus.Core</c>: a session identifier, recovery
    /// codes, the key-encryption keys, a link's code, an invitation's token, a
    /// generator's secret and its address, an app password, and the text of a notice
    /// in either language.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_LOG_003_AC1_TheLibrarysOwnCarriersAreReportedAsync()
    {
        string[] reported = await Analysis.OfAsync<NeverLoggedValueAnalyzer>("""
            using Janus.Core;
            using Microsoft.Extensions.Logging;

            namespace Cases;

            internal sealed class Host
            {
                internal static void Record(
                    ILogger logger,
                    SessionId session,
                    GeneratedRecoveryCodes codes,
                    KeyEncryptionKeys keys,
                    LinkLanding landing,
                    IssuedInvitation invitation,
                    GeneratorEnrolment enrolment,
                    IssuedAppPassword issued,
                    DocumentVersion notice,
                    DocumentTranslation translation)
                {
                    logger.LogInformation("{Session}", session);
                    logger.LogInformation("{Codes}", codes);
                    logger.LogInformation("{Keys}", keys);
                    logger.LogInformation("{Code}", landing.Code);
                    logger.LogInformation("{Token}", invitation.Token);
                    logger.LogInformation("{Secret}", enrolment.Secret);
                    logger.LogInformation("{Address}", enrolment.Address);
                    logger.LogInformation("{Secret}", issued.Secret);
                    logger.LogInformation("{Text}", notice.Text);
                    logger.LogInformation("{Text}", translation.Text);
                }
            }
            """);

        Assert.Equal(10, reported.Length);
        Assert.All(reported, rule => Assert.Equal("JAN0002", rule));
    }

    /// <summary>
    /// CONV-LOG-003 AC1: what may be logged in place of those is left alone: the
    /// notice's version, a landing's outcome and the invitation's identifier.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task CONV_LOG_003_AC1_TheVersionAndTheIdentifiersBesideThemAreNotReportedAsync()
    {
        string[] reported = await Analysis.OfAsync<NeverLoggedValueAnalyzer>("""
            using Janus.Core;
            using Microsoft.Extensions.Logging;

            namespace Cases;

            internal sealed class Host
            {
                internal static void Record(
                    ILogger logger,
                    LinkLanding landing,
                    IssuedInvitation invitation,
                    DocumentVersion notice)
                {
                    logger.LogInformation("{Version}", notice.Version);
                    logger.LogInformation("{Verified}", landing.Verified);
                    logger.LogInformation("{Invitation}", invitation.Id);
                }
            }
            """);

        Assert.Empty(reported);
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
