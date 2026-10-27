using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Who can access one record, the administrative view of chapter 09 section 8
/// (AUTHZ-DERIVE-007, AUTHZ-GATE-004), and which accounts may exercise a permission, of
/// section 8a (AUTHZ-GATE-007).
/// </summary>
[Trait("kind", "unit")]
public sealed class AccessEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser.
    /// </summary>
    public AccessEndpointTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// 09 section 8, AUTHZ-DERIVE-007 AC2: the view answers 200 with the record, the
    /// grants reaching it, and whether any derivation went unevaluated and which.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_TheViewAnswersTheRecordItsGrantsAndWhatWentUnevaluatedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Company, Permissions.GrantRead);

        Answer answered = await browser.SendAsync(
            "GET",
            $"/admin/access?resourceType=organization&resourceId={Company.Value}");

        JsonElement resource = answered.Json().GetProperty("resource");

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal("organization", resource.GetProperty("resourceType").GetString());
        Assert.Equal(Company.Value.ToString("D"), resource.GetProperty("resourceId").GetString());
        Assert.Equal(JsonValueKind.Array, answered.Json().GetProperty("grants").ValueKind);
        Assert.False(answered.Json().GetProperty("partial").GetBoolean());
        Assert.Equal(JsonValueKind.Array, answered.Json().GetProperty("unevaluated").ValueKind);
    }

    /// <summary>
    /// AUTHZ-DERIVE-007 AC1, 09 section 8: the view reports a stored grant and a derived
    /// one distinctly: each names its kind, the stored one carries its identifier and
    /// the derived one carries none, and each names the container it reaches the record
    /// through.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AC1_ADerivedGrantIsAnsweredWithNoIdentifierAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        var record = new ResourceReference(ResourceType.Parse("document"), ResourceId.Parse("quarterly"));
        var container = new ResourceReference(ResourceType.Parse("workspace"), ResourceId.Parse("finance"));
        var stored = GrantId.New(TimeProvider.System);
        var reviewing = Guid.NewGuid();

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Company, Permissions.GrantRead);
        _deployment.Gate.Register(
            Company,
            new ResourceAccess(
                record,
                [
                    new ExplainedGrant(stored, GrantKind.Stored, SubjectType.User, Guid.NewGuid(), RoleName.Parse("reader"), Deny: false, InheritedFrom: null),
                    new ExplainedGrant(Id: null, GrantKind.Derived, SubjectType.User, reviewing, RoleName.Parse("reviewer"), Deny: false, container),
                ],
                Partial: false,
                Unevaluated: []));

        Answer answered = await browser.SendAsync("GET", "/admin/access?resourceType=document&resourceId=quarterly");

        JsonElement[] grants = [.. answered.Json().GetProperty("grants").EnumerateArray()];

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal(("stored", stored.Value), (grants[0].GetProperty("kind").GetString(), grants[0].GetProperty("id").GetGuid()));
        Assert.Equal(JsonValueKind.Null, grants[0].GetProperty("inheritedFrom").ValueKind);
        Assert.Equal("derived", grants[1].GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, grants[1].GetProperty("id").ValueKind);
        Assert.Equal(reviewing, grants[1].GetProperty("subjectId").GetGuid());
        Assert.Equal("reviewer", grants[1].GetProperty("role").GetString());
        Assert.Equal("finance", grants[1].GetProperty("inheritedFrom").GetProperty("resourceId").GetString());
    }

    /// <summary>
    /// 09 section 8: a record not named by a type and an identifier is refused as
    /// malformed, naming the member that is missing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_ARecordNotNamedIsRefusedAsMalformedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer untyped = await browser.SendAsync("GET", "/admin/access?resourceId=one");
        Answer unnamed = await browser.SendAsync("GET", "/admin/access?resourceType=document&resourceId=%20");

        Assert.Equal(StatusCodes.Status400BadRequest, untyped.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), untyped.Text("code"));
        Assert.Equal("resourceType", untyped.Json().GetProperty("details").GetProperty("member").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, unnamed.Status);
        Assert.Equal("resourceId", unnamed.Json().GetProperty("details").GetProperty("member").GetString());
    }

    /// <summary>
    /// AUTHZ-DERIVE-007 AC3, 09 section 8: a record of a declared type the deployment
    /// holds no registration for reads as the gate's refusal, 403 <c>authz.denied</c>,
    /// even to a caller holding <c>grant:read</c>, and never as a malformed request.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_AC3_AnUnregisteredRecordReadsAsARefusalAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Company, Permissions.GrantRead);

        Answer refused = await browser.SendAsync(
            "GET",
            $"/admin/access?resourceType=document&resourceId={Guid.NewGuid()}");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005: a caller without <c>grant:read</c> is refused 403.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_007_TheViewIsRefusedWithoutGrantReadAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync(
            "GET",
            $"/admin/access?resourceType=organization&resourceId={Company.Value}");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
    }

    /// <summary>
    /// AUTHZ-GATE-007, 09 section 8a (D-218): which accounts may exercise a permission
    /// across an organization is answered 200 with the accounts, whether a derivation
    /// went unevaluated, and which.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_007_TheAccountsWhoMayAreAnsweredOverTheEndpointAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        var reading = new SubjectId(Guid.NewGuid());

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Company, Permissions.GrantRead);
        _deployment.Gate.Grant(reading, Company, Permissions.AccountRead);

        Answer answered = await browser.SendAsync(
            "GET",
            $"/admin/access/admitted?type=organization&id={Company.Value}&permission=account:read");

        Assert.Equal(StatusCodes.Status200OK, answered.Status);
        Assert.Equal(
            [reading.Value],
            [.. answered.Json().GetProperty("subjects").EnumerateArray().Select(subject => subject.GetGuid())]);
        Assert.False(answered.Json().GetProperty("partial").GetBoolean());
        Assert.Equal(0, answered.Json().GetProperty("unevaluated").GetArrayLength());
    }

    /// <summary>
    /// AUTHZ-GATE-007 AC5, 10 section 1 (D-219): a type, an organization's identifier or a
    /// permission the model does not declare is answered 400 <c>api.request.malformed</c>
    /// as the gate answers it, naming the member, and so is a permission that does not
    /// read as one.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_007_AC5_AWordTheModelDoesNotDeclareIsAnsweredAsMalformedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Company, Permissions.GrantRead);

        Answer untyped = await browser.SendAsync(
            "GET",
            "/admin/access/admitted?type=ledger&id=one&permission=account:read");
        Answer unnamed = await browser.SendAsync(
            "GET",
            "/admin/access/admitted?type=organization&id=one&permission=account:read");
        Answer undeclared = await browser.SendAsync(
            "GET",
            $"/admin/access/admitted?type=organization&id={Company.Value}&permission=ledger:read");
        Answer unread = await browser.SendAsync(
            "GET",
            $"/admin/access/admitted?type=organization&id={Company.Value}&permission=ledger");

        Assert.Equal(
            [
                ("type", ErrorCodes.RequestMalformed.ToString(), 400),
                ("id", ErrorCodes.RequestMalformed.ToString(), 400),
                ("permission", ErrorCodes.RequestMalformed.ToString(), 400),
                ("permission", ErrorCodes.RequestMalformed.ToString(), 400),
            ],
            new[] { untyped, unnamed, undeclared, unread }
                .Select(answer => (
                    answer.Json().GetProperty("details").GetProperty("member").GetString(),
                    answer.Text("code"),
                    answer.Status)));
    }

    /// <summary>
    /// AUTHZ-GATE-007, 09 section 8a (D-218): a caller without <c>grant:read</c> is refused
    /// 403 <c>authz.denied</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_007_TheAccountsWhoMayAreRefusedWithoutGrantReadAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer refused = await browser.SendAsync(
            "GET",
            $"/admin/access/admitted?type=organization&id={Company.Value}&permission=account:read");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), refused.Text("code"));
    }
}
