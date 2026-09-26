using System;
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
