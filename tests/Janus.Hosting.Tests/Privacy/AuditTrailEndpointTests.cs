using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Privacy;

/// <summary>
/// The audit trail read by subject over <c>GET /admin/audit</c> (PRIV-BREACH-002).
/// </summary>
[Trait("kind", "unit")]
public sealed class AuditTrailEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser, whose trail holds one customer's
    /// record.
    /// </summary>
    public AuditTrailEndpointTests()
    {
        Flow.Prepare(_deployment);
        _deployment.Administers(Company);
        _deployment.Trail.Hold(new AuditEntry(
            new AuditRecordId(Guid.CreateVersion7()),
            AuditCategory.Security,
            AuditAction.Parse("identity.account.suspended"),
            Noon,
            Ahmed,
            Ahmed,
            Organization: null,
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["reason"] = JsonSerializer.SerializeToElement("policy"),
            }));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// PRIV-BREACH-002: the records of the subject named are answered with their codes,
    /// identities and details.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BREACH_002_TheSubjectsRecordsAreReadOverTheEndpointAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer read = await browser.SendAsync("GET", $"/admin/audit?subject={Ahmed.Value}");

        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement entry = Assert.Single(read.Json().EnumerateArray());

        Assert.Equal("identity.account.suspended", entry.GetProperty("action").GetString());
        Assert.Equal("security", entry.GetProperty("category").GetString());
        Assert.Equal(Ahmed.Value, entry.GetProperty("effective").GetGuid());
        Assert.Equal("policy", entry.GetProperty("details").GetProperty("reason").GetString());
    }

    /// <summary>
    /// API-CONV-002: a subject that is absent or not an identifier is a malformed
    /// request, naming the member.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_BREACH_002_ASubjectThatIsNoIdentifierIsMalformedAsync()
    {
        Browser browser = await AuthorisedAsync();

        Answer read = await browser.SendAsync("GET", "/admin/audit?subject=ahmed");

        Assert.Equal(StatusCodes.Status400BadRequest, read.Status);
        Assert.Equal(ErrorCodes.RequestMalformed.ToString(), read.Text("code"));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-005 AC1: without <c>audit:read</c> the read is refused forbidden.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_005_AC1_TheReadIsRefusedWithoutThePermissionAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        Answer read = await browser.SendAsync("GET", $"/admin/audit?subject={Ahmed.Value}");

        Assert.Equal(StatusCodes.Status403Forbidden, read.Status);
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);

        _deployment.Gate.Grant(_deployment.Directory.Created[^1].Subject, Company, Permissions.AuditRead);

        return browser;
    }
}
