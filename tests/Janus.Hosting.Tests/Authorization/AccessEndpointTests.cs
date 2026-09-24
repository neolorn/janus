using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// Who can access one record, the administrative view of chapter 09 section 8
/// (AUTHZ-DERIVE-007, AUTHZ-GATE-004).
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
}
