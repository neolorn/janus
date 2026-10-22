using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Janus.Authentication.Callbacks;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Callbacks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Oidc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The SMS gateway's delivery report, the one callback endpoint the library defines,
/// reached as the gateway reaches it: on the machine profile, with its parameters in the
/// query string and nothing else (chapter 09 section 10, INT-SMS-005, BFF-MACH-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeliveryReportEndpointTests
{
    private const string Origin = "198.51.100.23";

    /// <summary>
    /// BFF-MACH-001 AC3 and INT-SMS-005 AC3: a report of failed delivery for a live
    /// send is carried on the machine profile and releases the send; the answer sets no
    /// cookie, because the browser profile never ran for it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC3_TheDeliveryReportIsCarriedOnTheMachineProfileAsync()
    {
        await using Deployment deployment = Prepared();

        string reference = await SentAsync(deployment);
        var destination = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Flow.Number);

        Assert.Single(deployment.SendLedger.Sends(destination));

        Answer reported = await new Machine(deployment)
            .CallAsync("/callbacks/sms/dlr?reference=" + reference + "&status=failed");

        Assert.Equal(StatusCodes.Status200OK, reported.Status);
        Assert.Empty(reported.SetCookie);
        Assert.Empty(deployment.SendLedger.Sends(destination));
    }

    /// <summary>
    /// Chapter 09 section 10 AC2, INT-SMS-005 AC3 and AUTH-ABUSE-004 AC20: a report that a
    /// text was delivered is taken and answered 200. For a registration's code, whose
    /// purpose is not <c>secondfactor</c>, it advances and releases nothing; for a send of
    /// purpose <c>secondfactor</c> it releases the send's one count from
    /// <c>sms.resend</c> and nothing else.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_SMS_005_AC3_ADeliveredReportReleasesOnlyASecondFactorSendsResendCountAsync()
    {
        await using Deployment deployment = Prepared();

        string reference = await SentAsync(deployment);
        var machine = new Machine(deployment);
        var destination = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Flow.Number);
        var resend = new RestrictionKey("sms.resend", RestrictionKeyKind.Destination, Flow.Number);

        Answer verification = await machine
            .CallAsync("/callbacks/sms/dlr?reference=" + reference + "&status=delivered");

        Assert.Equal(StatusCodes.Status200OK, verification.Status);
        Assert.Single(deployment.SendLedger.Sends(destination));
        Assert.Single(deployment.SendLedger.Sends(resend));

        const string other = "+201001234999";
        string second;

        await using (AsyncServiceScope scope = deployment.Scope())
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            _ = await work.BeginAsync(TestContext.Current.CancellationToken);

            Assert.True(PhoneNumber.TryParse(other, out PhoneNumber number));

            second = (await scope.ServiceProvider.GetRequiredService<IGovernedSend>().UndertakeAsync(
                    new OutboundMessage(
                        SendDestination.Of(number),
                        MessageKind.SecondStepCode,
                        RestrictionPurpose.SecondFactor,
                        "203.0.113.7",
                        "en")
                    {
                        Values = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = "123456" },
                    },
                    TestContext.Current.CancellationToken))
                .Match(sent => sent.Value, error => throw new Xunit.Sdk.XunitException($"The send was refused: {error.Code}."));

            _ = await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        Answer secondFactor = await machine
            .CallAsync("/callbacks/sms/dlr?reference=" + second + "&status=delivered");

        Assert.Equal(StatusCodes.Status200OK, secondFactor.Status);
        Assert.Single(deployment.SendLedger.Sends(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, other)));
        Assert.Empty(deployment.SendLedger.Sends(new RestrictionKey("sms.resend", RestrictionKeyKind.Destination, other)));
        Assert.Single(deployment.SendLedger.Sends(resend));
    }

    /// <summary>
    /// Chapter 09 section 10 AC1, INT-GEN-003 AC1 and INT-SMS-005 AC3: a report
    /// carrying a reference no send was given, and one the transport cannot read, are
    /// rejected and release nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_003_AC1_AForgedOrUnreadableReportIsRejectedAsync()
    {
        await using Deployment deployment = Prepared();

        _ = await SentAsync(deployment);
        var machine = new Machine(deployment);
        var destination = new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Flow.Number);

        Answer forged = await machine.CallAsync("/callbacks/sms/dlr?reference=AAAAAAAAAAAAAAAAAAAAAA&status=failed");
        Answer unreadable = await machine.CallAsync("/callbacks/sms/dlr?reference=AAAAAAAAAAAAAAAAAAAAAA");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, forged.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), forged.Text("code"));
        Assert.Null(forged.Header(HeaderNames.RetryAfter));
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, unreadable.Status);
        Assert.Null(unreadable.Header(HeaderNames.RetryAfter));
        Assert.Single(deployment.SendLedger.Sends(destination));
    }

    /// <summary>
    /// INT-GEN-003 AC3, INT-SMS-005 AC3 and BFF-MACH-003 AC2 (D-207): a report whose
    /// reference no held send answers, and one past the rate limit, are each logged as
    /// every callback route logs a rejection, naming the check and never the source; the
    /// first is recorded as a rejection against its source, the second is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_003_AC3_ARejectedReportIsRecordedAgainstItsSourceAndLoggedWithoutItAsync()
    {
        await using Deployment deployment = Prepared();

        _ = await SentAsync(deployment);
        var machine = new Machine(deployment) { From = IPAddress.Parse(Origin) };

        Answer forged = await machine.CallAsync("/callbacks/sms/dlr?reference=AAAAAAAAAAAAAAAAAAAAAA&status=failed");

        deployment.Configuration.Set(Settings.IntegrationCallbackRateLimit, 0);

        Answer flooded = await machine.CallAsync("/callbacks/sms/dlr?reference=AAAAAAAAAAAAAAAAAAAAAA&status=failed");

        string[] refused = [.. deployment.Logs.Lines.Where(line => line.Contains("Callback=sms/dlr", StringComparison.Ordinal))];

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, forged.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, flooded.Status);
        Assert.Equal(2, refused.Length);
        Assert.Contains("Check=Reference", refused[0], StringComparison.Ordinal);
        Assert.Contains("Check=RateLimit", refused[1], StringComparison.Ordinal);
        Assert.DoesNotContain(deployment.Logs.Lines, line => line.Contains(Origin, StringComparison.Ordinal));
        Assert.Single(Counted(deployment), callback => callback.Rejected && callback.Source == Origin);
    }

    /// <summary>
    /// BFF-MACH-001 AC2, chapter 09 section 10: a delivery report carrying a browser's
    /// session cookie is refused as every rejected callback is, 422 with no interval,
    /// is counted as a rejection, and releases nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC2_ADeliveryReportCarryingASessionCookieIsRejectedAndCountedAsync()
    {
        await using Deployment deployment = Prepared();

        string reference = await SentAsync(deployment);

        Answer refused = await new Machine(deployment).CallCarryingAsync(
            "/callbacks/sms/dlr?reference=" + reference + "&status=failed",
            BrowserCookies.Session + "=stale");

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, refused.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), refused.Text("code"));
        Assert.Null(refused.Header(HeaderNames.RetryAfter));
        Assert.Single(Counted(deployment), callback => callback.Rejected);
        Assert.Single(deployment.SendLedger.Sends(new RestrictionKey("sms.destination", RestrictionKeyKind.Destination, Flow.Number)));
    }

    /// <summary>
    /// INT-GEN-003 AC4 and BFF-MACH-001 AC2: a delivery report carrying a session
    /// cookie from a source past <c>integration.callback.ratelimit</c> is answered 429
    /// with the interval, as any report past it is, and is not counted as a rejection.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_GEN_003_AC4_ADeliveryReportCarryingASessionCookiePastTheRateLimitIsAnswered429Async()
    {
        await using Deployment deployment = Prepared();

        deployment.Configuration.Set(Settings.IntegrationCallbackRateLimit, 0);

        Answer flooded = await new Machine(deployment).CallCarryingAsync(
            "/callbacks/sms/dlr?reference=AAAAAAAAAAAAAAAAAAAAAA&status=failed",
            BrowserCookies.Session + "=stale");

        Assert.Equal(StatusCodes.Status429TooManyRequests, flooded.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), flooded.Text("code"));
        Assert.NotNull(flooded.Header(HeaderNames.RetryAfter));
        Assert.DoesNotContain(Counted(deployment), callback => callback.Rejected);
    }

    // Every callback the deployment counted, in order.
    private static List<(string Source, DateTimeOffset At, bool Rejected)> Counted(Deployment deployment)
    {
        using AsyncServiceScope scope = deployment.Scope();

        return ((CallbackLedgerInMemory)scope.ServiceProvider.GetRequiredService<ICallbackLedger>()).Counted;
    }

    // A deployment whose messages carry the code, as the shipped templates do.
    private static Deployment Prepared()
    {
        var deployment = new Deployment();

        Flow.Prepare(deployment);

        return deployment;
    }

    // A registration staging a phone once its address is verified, which sends the
    // code to it; the reference is what the gateway's report quotes.
    private static async Task<string> SentAsync(Deployment deployment)
    {
        Browser browser = await Flow.AwaitingAsync(deployment);

        await Flow.VerifiedAsync(deployment, browser, IdentifierKind.Email);

        _ = await browser.SendAsync("PUT", "/register/phone", ("value", Flow.Number));

        return deployment.Sms.Taken[^1].Reference;
    }
}
