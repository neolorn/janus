using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Authorization.Gate;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Storage.Authorization.Gate;
using Janus.Storage.Identity.Audit;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// What an admitted export leaves behind: its place in its actor's hour, and its own
/// row in the trail (OPS-ALERT-006, D-045).
/// </summary>
/// <remarks>
/// The port implementations are tested against the real database (D-156). One database
/// serves the class, so each test exports as an actor of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class ExportStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 9, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// OPS-ALERT-006, D-045: an actor's hour holds that actor's exports alone, a person
    /// and a system principal apart, oldest first, and an export older than the hour is
    /// forgotten when the actor's next one is recorded.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_ALERT_006_EachActorsHourHoldsItsOwnExportsAsync()
    {
        var clerk = new SubjectId(Guid.CreateVersion7());
        var manager = new SubjectId(Guid.CreateVersion7());
        string nightly = "nightly-" + Guid.CreateVersion7().ToString("N");

        await RecordedAsync(clerk, principal: null, Noon.AddMinutes(-90));
        await RecordedAsync(clerk, principal: null, Noon.AddMinutes(-30));
        await RecordedAsync(manager, principal: null, Noon.AddMinutes(-20));
        await RecordedAsync(actor: null, nightly, Noon.AddMinutes(-10));

        Assert.Equal([Noon.AddMinutes(-30)], await SinceAsync(clerk, principal: null, Noon.AddHours(-1)));
        Assert.Equal([Noon.AddMinutes(-20)], await SinceAsync(manager, principal: null, Noon.AddHours(-1)));
        Assert.Equal([Noon.AddMinutes(-10)], await SinceAsync(actor: null, nightly, Noon.AddHours(-1)));

        await RecordedAsync(clerk, principal: null, Noon);

        await using StoreContext reading = database.Context();

        List<DateTimeOffset> held = await reading.BulkExports
            .Where(export => export.Actor == clerk)
            .OrderBy(export => export.AdmittedAt)
            .Select(export => export.AdmittedAt)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal([Noon.AddMinutes(-30), Noon], held);
    }

    /// <summary>
    /// OPS-ALERT-006, D-045: an admitted export is recorded on its own, as a security
    /// event naming who exported, the operation, the kind of record and the one record
    /// the call named, and a system principal's under its name and its reason.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_ALERT_006_AnAdmittedExportIsRecordedOnItsOwnAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId clerk = await _deployment.AccountAsync(now);
        OrganizationId organization = await _deployment.OrganizationAsync(now);
        var export = Permission.Parse("document:export");
        var documents = ResourceType.Parse("document");
        var nightly = SystemPrincipal.ForOrganization("nightly-report", "the nightly report", organization);

        await using (StoreContext writing = database.Context())
        {
            var audit = new AccessAudit(new DataConnections(writing));

            await audit.RecordAsync(
                new ExportedAccess(
                    AuditRecordId.New(TimeProvider.System),
                    clerk,
                    clerk,
                    Principal: null,
                    organization,
                    export,
                    documents,
                    ResourceId.Parse("quarterly"),
                    now),
                TestContext.Current.CancellationToken);

            await audit.RecordAsync(
                new ExportedAccess(
                    AuditRecordId.New(TimeProvider.System),
                    Acting: null,
                    Effective: null,
                    nightly,
                    organization,
                    export,
                    documents,
                    Record: null,
                    now),
                TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(clerk));

        Assert.Equal(AuditActions.AccessExported, read.Action);
        Assert.Equal(AuditCategory.Security, read.Category);
        Assert.Equal(clerk, read.ActingSubject);
        Assert.Equal(clerk, read.EffectiveSubject);
        Assert.Equal(organization, read.Organization);
        Assert.Null(read.Principal);
        Assert.Equal(
            ["permission", "resource", "resourceType"],
            read.Details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("document:export", read.Details["permission"].GetString());
        Assert.Equal("document", read.Details["resourceType"].GetString());
        Assert.Equal("quarterly", read.Details["resource"].GetString());

        await using StoreContext reading = database.Context();

        Assert.Equal(
            1,
            await reading.AuditRecords.CountAsync(
                row => row.Principal == "nightly-report"
                    && row.PrincipalReason == "the nightly report"
                    && row.Organization == organization,
                TestContext.Current.CancellationToken));
    }

    private async Task RecordedAsync(SubjectId? actor, string? principal, DateTimeOffset at)
    {
        await using StoreContext context = database.Context();

        await new BulkExportLedger(new DataConnections(context))
            .RecordAsync(actor, principal, at, at.AddHours(-1), TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<DateTimeOffset>> SinceAsync(
        SubjectId? actor,
        string? principal,
        DateTimeOffset since)
    {
        await using StoreContext context = database.Context();

        return await new BulkExportLedger(new DataConnections(context))
            .SinceAsync(actor, principal, since, TestContext.Current.CancellationToken);
    }

    private async Task<IReadOnlyList<AuditRecord>> RecordsAsync(SubjectId actor)
    {
        await using StoreContext reading = database.Context();

        return await new AuditStore(reading, new DataConnections(reading), _deployment.Keys, _deployment.Randomness)
            .FindBySubjectAsync(actor, TestContext.Current.CancellationToken);
    }
}
