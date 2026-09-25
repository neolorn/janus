using System;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The two resolutions of a refusal's correlation identifier of chapter 09 section 8a:
/// the support role's, and the caller's own (AUTHZ-GATE-004, AUTHZ-CONCEAL-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class ExplanationEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private readonly Janus.Hosting.Tests.Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, administered by one organization.
    /// </summary>
    public ExplanationEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Administration);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// AUTHZ-GATE-004 AC3: the caller resolves the identifier of their own refusal on an
    /// operation tied to no record, and the answer names the permission and the
    /// principal and no grant.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC3_TheCallerResolvesTheirOwnRefusalOverTheEndpointAsync()
    {
        (Browser browser, SubjectId subject, Guid correlation) = await RefusedAsync();

        Answer resolved = await browser.SendAsync("GET", $"/account/explanations/{correlation}");

        Assert.Equal(StatusCodes.Status200OK, resolved.Status);
        Assert.Equal("denied", resolved.Text("outcome"));
        Assert.Equal(Permissions.SessionRevoke.ToString(), resolved.Text("permission"));
        Assert.Equal(subject.Value, resolved.Json().GetProperty("principal").GetProperty("acting").GetGuid());
        Assert.Equal(subject.Value, resolved.Json().GetProperty("principal").GetProperty("effective").GetGuid());
        Assert.Equal(JsonValueKind.Null, resolved.Json().GetProperty("grant").ValueKind);
    }

    /// <summary>
    /// AUTHZ-GATE-004 AC3: an identifier that stands for no refusal of the caller is
    /// refused.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC3_AnIdentifierOfNoRefusalOfTheCallersIsRefusedAsync()
    {
        (Browser browser, _, _) = await RefusedAsync();

        Answer resolved = await browser.SendAsync("GET", $"/account/explanations/{Guid.NewGuid()}");

        Assert.Equal(StatusCodes.Status403Forbidden, resolved.Status);
        Assert.Equal(ErrorCodes.Denied.ToString(), resolved.Text("code"));
    }

    /// <summary>
    /// AUTHZ-GATE-004 AC4, AUTHZ-CONCEAL-005 AC1: the support resolution is refused
    /// forbidden without <c>audit:read</c> in the administrative organization, and
    /// answers the refusal with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_004_AC4_TheSupportResolutionNeedsTheReadRoleAsync()
    {
        (Browser browser, SubjectId subject, Guid correlation) = await RefusedAsync();

        Answer without = await browser.SendAsync("GET", $"/admin/explanations/{correlation}");

        _deployment.Gate.Grant(subject, Administration, Permissions.AuditRead);

        Answer with = await browser.SendAsync("GET", $"/admin/explanations/{correlation}");

        Assert.Equal(StatusCodes.Status403Forbidden, without.Status);
        Assert.Equal(StatusCodes.Status200OK, with.Status);
        Assert.Equal("denied", with.Text("outcome"));
        Assert.Equal(Permissions.SessionRevoke.ToString(), with.Text("permission"));
    }

    // A browser refused an operation of the deployment, and the identifier the gate
    // recorded the refusal under.
    private async Task<(Browser Browser, SubjectId Subject, Guid Correlation)> RefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        Answer refused = await browser.SendAsync("POST", "/admin/sessions/revoke-all");

        Assert.Equal(StatusCodes.Status403Forbidden, refused.Status);

        return (browser, subject, _deployment.Gate.Refusals[^1].Value);
    }
}
