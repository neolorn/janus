using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Janus.Hosting.Tests.Bff;

/// <summary>
/// The stage that holds an endpoint answering only a signed-in person to having a
/// session, and what the stage before it does with a cookie that no longer resolves
/// (BFF-ORDER-001 stage 5, BFF-STEP-001, API-CONV-003).
/// </summary>
[Trait("kind", "unit")]
public sealed class SessionRequirementTests : IAsyncDisposable
{
    // Every endpoint the library mounts that answers nobody, by method and path. A
    // new endpoint that needs a session is added here as it is mounted, and one that
    // does not is kept out of it: the stage refuses exactly this list.
    private static readonly string[] Held =
    [
        "DELETE /account/devices/{id:guid}",
        "DELETE /account/identifiers/{id:guid}",
        "DELETE /account/photo",
        "DELETE /account/sessions/{id:guid}",
        "DELETE /admin/grants/{id:guid}",
        "DELETE /admin/groups/{id:guid}",
        "DELETE /admin/groups/{id:guid}/members",
        "DELETE /admin/organizations/{id:guid}/domains/{domain}",
        "DELETE /admin/organizations/{id:guid}/invitations/{invitationId:guid}",
        "DELETE /admin/organizations/{id:guid}/memberships/{subject:guid}",
        "DELETE /admin/restrictions/{name}",
        "DELETE /admin/roles/{name}",
        "DELETE /privacy/objections/{purpose}",
        "GET /account/",
        "GET /account/credentials",
        "GET /account/devices/",
        "GET /account/explanations/{correlationId:guid}",
        "GET /account/invitation",
        "GET /account/photo",
        "GET /account/preferences",
        "GET /account/sessions",
        "GET /admin/accounts/{subject:guid}/takedown/",
        "GET /admin/audit",
        "GET /admin/config/{key}",
        "GET /admin/explanations/{correlationId:guid}",
        "GET /admin/groups",
        "GET /admin/organizations/{id:guid}/domains",
        "GET /admin/organizations/{id:guid}/policy",
        "GET /admin/privacy/requests/",
        "GET /admin/ropa",
        "GET /admin/restrictions/",
        "GET /admin/restrictions/{name}",
        "GET /admin/roles",
        "GET /auth/session",
        "GET /privacy/consents",
        "GET /privacy/export",
        "GET /privacy/objections",
        "PATCH /account/credentials/{id:guid}",
        "POST /account/deactivate",
        "POST /account/delete",
        "POST /account/identifiers",
        "POST /account/identifiers/{id:guid}/primary",
        "POST /account/invitation/acknowledge",
        "POST /admin/accounts/{subject:guid}/reactivate",
        "POST /admin/accounts/{subject:guid}/restriction/lift",
        "POST /admin/accounts/{subject:guid}/suspend",
        "POST /admin/accounts/{subject:guid}/takedown/",
        "POST /admin/accounts/{subject:guid}/takedown/reverse",
        "POST /admin/accounts/{subject:guid}/sessions/revoke",
        "POST /admin/documents/{document}/versions",
        "POST /admin/grants",
        "POST /admin/groups",
        "POST /admin/groups/{id:guid}/members",
        "POST /admin/notices",
        "POST /admin/organizations",
        "POST /admin/organizations/{id:guid}/delete",
        "POST /admin/organizations/{id:guid}/delete/cancel",
        "POST /admin/organizations/{id:guid}/domains",
        "POST /admin/organizations/{id:guid}/domains/{domain}/verify",
        "POST /admin/organizations/{id:guid}/invitations",
        "POST /admin/privacy/requests/",
        "POST /admin/privacy/requests/{request:guid}/fulfil",
        "POST /admin/privacy/requests/{request:guid}/refuse",
        "POST /admin/recovery/approve",
        "POST /admin/restrictions/{name}/grant",
        "POST /admin/roles",
        "POST /admin/sessions/revoke-all",
        "POST /auth/logout",
        "POST /auth/step-up",
        "POST /privacy/consents/{purpose}/grant",
        "POST /privacy/consents/{purpose}/withdraw",
        "POST /privacy/objections/{purpose}",
        "POST /privacy/requests",
        "POST /recovery/report-loss",
        "PUT /account/photo",
        "PUT /account/preferences",
        "PUT /account/profile",
        "PUT /account/identifiers/backup",
        "PUT /account/secondstep/preferred",
        "PUT /admin/compliance/assessments",
        "PUT /admin/config/{key}",
        "PUT /admin/organizations/{id:guid}/policy",
        "PUT /admin/restrictions/{name}",
        "PUT /admin/documents/{document}/versions/{version}/translations/{language}",
    ];

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment a browser can register against, which is how a test here gets a
    /// session to let expire.
    /// </summary>
    public SessionRequirementTests() => Flow.Prepare(_deployment);

    /// <summary>
    /// BFF-STEP-001: which endpoints answer only a signed-in person is fixed where
    /// they are mounted, so an endpoint added later inherits nothing by accident and
    /// is refused nothing by accident.
    /// </summary>
    [Fact]
    public void BFF_STEP_001_TheEndpointsThatNeedASessionAreTheOnesListed()
    {
        Assert.Equal(
            Held.Order(StringComparer.Ordinal),
            _deployment.Endpoints
                .Where(SessionRequired.Asks)
                .Select(Named)
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// BFF-STEP-001 AC3: an endpoint that needs a session answers a browser whose
    /// session has ended with what has to be done again, and clears the pair it
    /// answered to rather than leaving the browser presenting it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_STEP_001_AC3_AnEndedSessionIsAnsweredWithWhatMustBeRedoneAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromDays(100));

        Answer answered = await browser.SendAsync("GET", "/account/");

        Assert.Equal(StatusCodes.Status401Unauthorized, answered.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), answered.Text("code"));
        Assert.NotEmpty(
            answered.Json().GetProperty("details").GetProperty("reauthenticate").GetString()!);
        Assert.DoesNotContain(BrowserCookies.Session, browser.Cookies.Keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// BFF-ORDER-001 stage 5: a cookie that no longer resolves leaves the request
    /// anonymous rather than refusing it, so the endpoints a person whose session
    /// ended has to reach are reachable while the browser still carries it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task BFF_ORDER_001_AnEndedSessionDoesNotRefuseAnEndpointThatNeedsNoneAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Clock.Advance(TimeSpan.FromDays(100));

        Answer begun = await browser.SendAsync(
            "POST",
            "/auth/begin",
            ("identifier", Flow.Address),
            ("clientId", "web"));

        Assert.NotEqual(StatusCodes.Status401Unauthorized, begun.Status);
    }

    /// <summary>
    /// API-CONV-003: a browser that never held a session is answered by the same
    /// stage, with the code alone, because it has nothing to do again.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task API_CONV_003_ABrowserThatHeldNoSessionIsAnsweredWithTheCodeAloneAsync()
    {
        Answer answered = await new Browser(_deployment).SendAsync("GET", "/account/");

        Assert.Equal(StatusCodes.Status401Unauthorized, answered.Status);
        Assert.Equal(ErrorCodes.SessionExpired.ToString(), answered.Text("code"));
        Assert.Empty(answered.Json().GetProperty("details").EnumerateObject());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _deployment.DisposeAsync();

    private static string Named(Endpoint endpoint) =>
        string.Join(
            ", ",
            endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Order(StringComparer.Ordinal))
        + " "
        + ((RouteEndpoint)endpoint).RoutePattern.RawText;
}
