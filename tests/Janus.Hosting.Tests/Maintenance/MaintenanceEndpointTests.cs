using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Janus.Hosting.Tests.Maintenance;

/// <summary>
/// The licences and permits and the maintenance log over the compliance endpoints of
/// chapter 09 section 8a (OPS-MAINT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class MaintenanceEndpointTests : IAsyncDisposable
{
    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly Guid Operating = Guid.Parse("0199a0b1-0000-7000-8000-000000000001");

    private static readonly Guid Premises = Guid.Parse("0199a0b1-0000-7000-8000-000000000002");

    private readonly Deployment _deployment = new();

    /// <summary>
    /// A deployment able to register a browser.
    /// </summary>
    public MaintenanceEndpointTests() => Flow.Prepare(_deployment);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _deployment.DisposeAsync();

    /// <summary>
    /// OPS-MAINT-001 AC1: the licences and permits put over the endpoint read back
    /// with their expiry dates, soonest to lapse first.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC1_TheExpiryDatesAreStoredAndReadBackAsync()
    {
        Browser browser = await AuthorisedAsync();
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        Answer replaced = await browser.SendAsync(
            "PUT",
            "/admin/compliance/licences",
            (
                "licences",
                new object[]
                {
                    new { id = Operating, kind = "licence", name = "Operating licence", expiresAt = now.AddYears(2) },
                    new
                    {
                        id = Premises,
                        kind = "permit",
                        name = "Premises permit",
                        expiresAt = now.AddMonths(5),
                        renewedAt = now.AddMonths(-7),
                    },
                }));

        Assert.Equal(StatusCodes.Status204NoContent, replaced.Status);

        Answer read = await browser.SendAsync("GET", "/admin/compliance/licences");

        Assert.Equal(StatusCodes.Status200OK, read.Status);

        JsonElement[] licences = [.. read.Json().GetProperty("licences").EnumerateArray()];

        Assert.Equal([Premises, Operating], licences.Select(licence => licence.GetProperty("id").GetGuid()));
        Assert.Equal("permit", licences[0].GetProperty("kind").GetString());
        Assert.Equal(now.AddMonths(5), licences[0].GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(now.AddMonths(-7), licences[0].GetProperty("renewedAt").GetDateTimeOffset());
        Assert.Equal(now.AddYears(2), licences[1].GetProperty("expiresAt").GetDateTimeOffset());
    }

    /// <summary>
    /// OPS-MAINT-001 AC3: a task recorded over the endpoint is dated and carries the
    /// signed-in person as its actor, and the log reads it back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AC3_ARecordedTaskIsDatedAndCarriesThePersonAskingAsync()
    {
        Browser browser = await AuthorisedAsync();
        SubjectId subject = _deployment.Directory.Created[^1].Subject;
        DateTimeOffset performedAt = _deployment.Clock.GetUtcNow().AddDays(-1);

        Answer recorded = await browser.SendAsync(
            "POST",
            "/admin/compliance/maintenance",
            ("task", "envelope-rotation"),
            ("performedAt", performedAt),
            ("note", "Rotated with the release."));

        Assert.Equal(StatusCodes.Status201Created, recorded.Status);
        Assert.Equal(subject.Value, recorded.Json().GetProperty("actor").GetGuid());

        Answer log = await browser.SendAsync("GET", "/admin/compliance/maintenance");

        Assert.Equal(StatusCodes.Status200OK, log.Status);

        JsonElement entry = Assert.Single(log.Json().GetProperty("entries").EnumerateArray());

        Assert.Equal("envelope-rotation", entry.GetProperty("task").GetString());
        Assert.Equal(performedAt, entry.GetProperty("performedAt").GetDateTimeOffset());
        Assert.Equal(subject.Value, entry.GetProperty("actor").GetGuid());
        Assert.Equal("Rotated with the release.", entry.GetProperty("note").GetString());
    }

    /// <summary>
    /// OPS-MAINT-001: a body the endpoint cannot read is malformed at the member that
    /// stopped it, and nothing is stored.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_ABodyThatCannotBeReadIsMalformedAsync()
    {
        Browser browser = await AuthorisedAsync();
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        Answer unnamed = await browser.SendAsync(
            "PUT",
            "/admin/compliance/licences",
            ("licences", new object[] { new { id = Operating, kind = "lease", name = "Lease", expiresAt = now } }));

        Assert.Equal(StatusCodes.Status400BadRequest, unnamed.Status);
        Assert.Equal("kind", unnamed.Json().GetProperty("details").GetProperty("member").GetString());

        Answer unknown = await browser.SendAsync(
            "POST",
            "/admin/compliance/maintenance",
            ("task", "window-cleaning"),
            ("performedAt", now));

        Assert.Equal(StatusCodes.Status400BadRequest, unknown.Status);
        Assert.Equal("task", unknown.Json().GetProperty("details").GetProperty("member").GetString());

        Assert.Empty(await _deployment.Maintenance.LicencesAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_deployment.Maintenance.Log);
    }

    /// <summary>
    /// OPS-MAINT-001 and 10 section 2.1: every route answers to
    /// <c>compliance:manage</c>, so a signed-in account without it is refused each.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_MAINT_001_AnAccountWithoutComplianceManageIsRefusedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        DateTimeOffset now = _deployment.Clock.GetUtcNow();

        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync("GET", "/admin/compliance/licences")).Status);
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync(
                "PUT",
                "/admin/compliance/licences",
                ("licences", Array.Empty<object>()))).Status);
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync("GET", "/admin/compliance/maintenance")).Status);
        Assert.Equal(
            StatusCodes.Status403Forbidden,
            (await browser.SendAsync(
                "POST",
                "/admin/compliance/maintenance",
                ("task", "approver-review"),
                ("performedAt", now))).Status);

        Assert.Empty(_deployment.Maintenance.Log);
    }

    private async Task<Browser> AuthorisedAsync()
    {
        Browser browser = await Flow.SignedInAsync(_deployment);
        SubjectId subject = _deployment.Directory.Created[^1].Subject;

        _deployment.Administers(Company);
        _deployment.Gate.Grant(subject, Company, Permissions.ComplianceManage);

        return browser;
    }
}
