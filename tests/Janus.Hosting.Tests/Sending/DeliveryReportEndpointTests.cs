using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Hosting.Tests.Oidc;
using Microsoft.AspNetCore.Http;
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
        var destination = new RestrictionKey("sms.destination", Flow.Number);

        Assert.Single(deployment.SendLedger.Sends(destination));

        Answer reported = await new Machine(deployment)
            .CallAsync("/callbacks/sms/dlr?reference=" + reference + "&status=failed");

        Assert.Equal(StatusCodes.Status200OK, reported.Status);
        Assert.Empty(reported.SetCookie);
        Assert.Empty(deployment.SendLedger.Sends(destination));
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
        var destination = new RestrictionKey("sms.destination", Flow.Number);

        Answer forged = await machine.CallAsync("/callbacks/sms/dlr?reference=AAAAAAAAAAAAAAAAAAAAAA&status=failed");
        Answer unreadable = await machine.CallAsync("/callbacks/sms/dlr?reference=AAAAAAAAAAAAAAAAAAAAAA");

        Assert.Equal(StatusCodes.Status429TooManyRequests, forged.Status);
        Assert.Equal(ErrorCodes.CallbackRejected.ToString(), forged.Text("code"));
        Assert.Equal(StatusCodes.Status429TooManyRequests, unreadable.Status);
        Assert.Single(deployment.SendLedger.Sends(destination));
    }

    /// <summary>
    /// BFF-MACH-001 AC2: the delivery report refuses a request carrying a browser's
    /// session cookie, as every machine route does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_MACH_001_AC2_ADeliveryReportCarryingASessionCookieIsRefusedAsync()
    {
        await using Deployment deployment = Prepared();

        string reference = await SentAsync(deployment);

        Answer refused = await new Machine(deployment).CallCarryingAsync(
            "/callbacks/sms/dlr?reference=" + reference + "&status=failed",
            BrowserCookies.Session + "=stale");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Single(deployment.SendLedger.Sends(new RestrictionKey("sms.destination", Flow.Number)));
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
