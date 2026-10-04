using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The flood limit per source address, which answers before anything reads a session
/// (BFF-ORDER-001 stage 4).
/// </summary>
[Trait("kind", "unit")]
public sealed class SourceRateLimitingTests
{
    private static readonly IPAddress Flooding = IPAddress.Parse("198.51.100.7");

    private static readonly IPAddress Neighbour = IPAddress.Parse("203.0.113.9");

    /// <summary>
    /// BFF-ORDER-001 AC3: more requests than the limit from one source inside the
    /// minute are refused with the instant the source is admitted again, before the
    /// session is looked up: a new browser is given no first contact and no cookie,
    /// and a browser that already holds one is refused all the same.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC3_AFloodFromOneSourceIsRefusedBeforeSessionLookupAsync()
    {
        await using var deployment = new Deployment();
        var holding = new Browser(deployment);
        DateTimeOffset began = deployment.Clock.GetUtcNow();

        deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 3);

        Answer first = await holding.SendAsync("GET", "/register", source: Flooding);
        Answer second = await new Browser(deployment).SendAsync("GET", "/register", source: Flooding);
        Answer third = await new Browser(deployment).SendAsync("GET", "/register", source: Flooding);

        Assert.All(
            new[] { first, second, third },
            admitted => Assert.NotEqual(StatusCodes.Status429TooManyRequests, admitted.Status));
        Assert.Equal(3, deployment.Contacts.All.Count);

        Answer fresh = await new Browser(deployment).SendAsync("GET", "/register", source: Flooding);
        Answer held = await holding.SendAsync("GET", "/register", source: Flooding);

        foreach (Answer refused in new[] { fresh, held })
        {
            Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Status);
            Assert.Equal(ErrorCodes.Throttled.ToString(), refused.Text("code"));
            Assert.Equal(began.AddMinutes(1), RetryAt(refused));
            Assert.Equal("60", refused.Header(HeaderNames.RetryAfter));
            Assert.Empty(refused.SetCookie);
        }

        Assert.Equal(3, deployment.Contacts.All.Count);
    }

    /// <summary>
    /// BFF-ORDER-001 AC3: the limit is counted per source, so a source over it does
    /// not hold back a neighbour that is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC3_AnotherSourceIsNotRefusedAsync()
    {
        await using var deployment = new Deployment();

        deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 1);

        Answer admitted = await new Browser(deployment).SendAsync("GET", "/register", source: Flooding);
        Answer refused = await new Browser(deployment).SendAsync("GET", "/register", source: Flooding);
        Answer neighbour = await new Browser(deployment).SendAsync("GET", "/register", source: Neighbour);

        Assert.NotEqual(StatusCodes.Status429TooManyRequests, admitted.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Status);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, neighbour.Status);
        Assert.Equal(2, deployment.Contacts.All.Count);
    }

    /// <summary>
    /// BFF-ORDER-001 AC3: the window slides. A request is counted for the minute after
    /// it, so the source is admitted again the instant its oldest request leaves the
    /// window and not before, and the request it made half a minute later still
    /// counts after that.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC3_TheWindowResetsAsItsRequestsLeaveItAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);
        DateTimeOffset began = deployment.Clock.GetUtcNow();

        deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 2);

        Answer oldest = await browser.SendAsync("GET", "/register", source: Flooding);

        deployment.Clock.Advance(TimeSpan.FromSeconds(30));

        Answer later = await browser.SendAsync("GET", "/register", source: Flooding);
        Answer over = await browser.SendAsync("GET", "/register", source: Flooding);

        deployment.Clock.Advance(TimeSpan.FromSeconds(30) - TimeSpan.FromMilliseconds(1));

        Answer early = await browser.SendAsync("GET", "/register", source: Flooding);

        deployment.Clock.Advance(TimeSpan.FromMilliseconds(1));

        Answer again = await browser.SendAsync("GET", "/register", source: Flooding);
        Answer full = await browser.SendAsync("GET", "/register", source: Flooding);

        Assert.NotEqual(StatusCodes.Status429TooManyRequests, oldest.Status);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, later.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, over.Status);
        Assert.Equal(began.AddMinutes(1), RetryAt(over));
        Assert.Equal("30", over.Header(HeaderNames.RetryAfter));
        Assert.Equal(StatusCodes.Status429TooManyRequests, early.Status);
        Assert.Equal("1", early.Header(HeaderNames.RetryAfter));
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, again.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, full.Status);
        Assert.Equal(began.AddSeconds(30).AddMinutes(1), RetryAt(full));
    }

    /// <summary>
    /// BFF-ORDER-001 AC3, AC6: every address of one IPv6 /64 is one source, so requests
    /// spread across a subnet are counted together and refused past the one limit.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC3_AddressesInOneIpv6SubnetAreOneSourceAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 2);

        Answer first = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1::1"));
        Answer second = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1:ffff::2"));
        Answer third = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1::3"));

        Assert.NotEqual(StatusCodes.Status429TooManyRequests, first.Status);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, second.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, third.Status);
        Assert.Equal(ErrorCodes.Throttled.ToString(), third.Text("code"));
    }

    /// <summary>
    /// BFF-ORDER-001 AC3, AC7: another /64 of the same /48 is a source of its own and
    /// is admitted until the /48 reaches <c>abuse.source.sitelimit</c>; past it every
    /// subnet of the site is refused, and another site is not.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC3_AnotherSubnetOfTheSiteIsAdmittedUntilTheSiteLimitAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 1);
        deployment.Configuration.Set(Settings.AbuseSourceSiteLimit, 2);

        Answer first = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1::1"));
        Answer held = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1::2"));
        Answer second = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:2::1"));
        Answer third = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:3::1"));
        Answer elsewhere = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:2:1::1"));

        Assert.NotEqual(StatusCodes.Status429TooManyRequests, first.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, held.Status);
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, second.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, third.Status);
        Assert.Equal(ErrorCodes.Throttled.ToString(), third.Text("code"));
        Assert.NotEqual(StatusCodes.Status429TooManyRequests, elsewhere.Status);
    }

    /// <summary>
    /// BFF-ORDER-001 AC3, AC6: an IPv4 address that arrives mapped into IPv6 is the
    /// source its IPv4 address is, and is counted by no /48.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC3_AnIpv4MappedAddressIsItsIpv4SourceAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 1);
        deployment.Configuration.Unreachable = Settings.AbuseSourceSiteLimit.Key;

        Answer plain = await browser.SendAsync("GET", "/register", source: Flooding);
        Answer mapped = await browser.SendAsync("GET", "/register", source: Flooding.MapToIPv6());

        Assert.NotEqual(StatusCodes.Status429TooManyRequests, plain.Status);
        Assert.Equal(StatusCodes.Status429TooManyRequests, mapped.Status);
    }

    /// <summary>
    /// BFF-ORDER-001 AC3: a source or a /48 going over its limit writes one line when
    /// its hold begins, and each request refused while it is held writes none.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AC3_AHeldSourceWritesNoLineForEachRefusalAsync()
    {
        await using var deployment = new Deployment();
        var browser = new Browser(deployment);

        deployment.Configuration.Set(Settings.AbuseSourceRateLimit, 1);
        deployment.Configuration.Set(Settings.AbuseSourceSiteLimit, 2);

        _ = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1::1"));

        int before = Held();

        _ = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1::2"));

        int begun = Held();

        _ = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:1::3"));
        _ = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:2::1"));
        _ = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:3::1"));

        int siteBegun = Held();

        Answer refused = await browser.SendAsync("GET", "/register", source: IPAddress.Parse("2001:db8:1:4::1"));

        Assert.Equal(before + 1, begun);
        Assert.Equal(begun + 1, siteBegun);
        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Status);
        Assert.Equal(siteBegun, Held());

        int Held() => deployment.Logs.Lines.Count(line =>
            line.Contains("went over its request limit", StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTH-SESS-013 AC6: a sign-in from an IPv6 address opens a session that records
    /// the whole address, never the /64 its failures are counted by.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_SESS_013_TheSessionRecordsTheWholeAddressAsync()
    {
        await using var deployment = new Deployment();
        var whole = IPAddress.Parse("2001:db8:1:1::7");

        Flow.Prepare(deployment);
        deployment.Configuration.Set(Settings.DeviceVerificationEnabled, false);

        _ = await Flow.SignedInAsync(deployment);

        deployment.Clock.Advance(TimeSpan.FromMinutes(5));

        var browser = new Browser(deployment);

        _ = await browser.SendAsync("GET", "/auth/session", source: whole);

        Answer began = await browser.SendAsync(whole, "trace-begin", "POST", "/auth/begin", ("identifier", Flow.Address));
        string challenge = began.Text("challengeId");

        Answer wrong = await browser.SendAsync(
            whole,
            "trace-wrong",
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", "not the password at all"));
        Answer signedIn = await browser.SendAsync(
            whole,
            "trace-right",
            "POST",
            "/auth/factor",
            ("challengeId", challenge),
            ("factor", "password"),
            ("value", Flow.Password));

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, wrong.Status);
        Assert.Equal("complete", signedIn.Text("status"));
        Assert.Contains(deployment.Sessions.All, session => session.Origin.Address == "2001:db8:1:1::7");
        Assert.DoesNotContain(deployment.Sessions.All, session => session.Origin.Address.EndsWith("/64", StringComparison.Ordinal));
        Assert.Contains(deployment.Sessions.All, session => session.Origin.Source == "2001:db8:1:1::/64");
    }

    /// <summary>
    /// AUTH-PRIN-001 AC3, BFF-ORDER-001 stage 4: a limit the store cannot give is not
    /// taken as no limit. The request is answered as a fault and goes no further, so no
    /// session is looked for and no first contact is given.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_PRIN_001_AC3_ALimitThatCannotBeReadAdmitsNothingAsync()
    {
        await using var deployment = new Deployment();

        deployment.Configuration.Unreachable = Settings.AbuseSourceRateLimit.Key;

        Answer refused = await new Browser(deployment).SendAsync("GET", "/register", source: Flooding);

        Assert.Equal(StatusCodes.Status500InternalServerError, refused.Status);
        Assert.Equal(ErrorCodes.SystemFault.ToString(), refused.Text("code"));
        Assert.Empty(refused.SetCookie);
        Assert.Empty(deployment.Contacts.All);
    }

    private static DateTimeOffset RetryAt(Answer answer) =>
        answer.Json().GetProperty("details").GetProperty("retryAt").GetDateTimeOffset();
}
