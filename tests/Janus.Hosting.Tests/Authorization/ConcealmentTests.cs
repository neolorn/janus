using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What a refusal costs the database, which must say no more about the record than the
/// answer does (AUTHZ-CONCEAL-002, BFF-ERR-003).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class ConcealmentTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Report = ResourceType.Parse("report");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// AUTHZ-CONCEAL-002 AC2, BFF-ERR-003, D-166: the refusal of a record the library
    /// holds no row for and the refusal of a registered record issue the same
    /// statements with the same texts, on a type the stored grants alone decide and on
    /// one a derivation reaches, asked with its sources.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_002_AC2_AnAbsentRecordAndARefusedOneRunTheSameStatementsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        await deployment.BeginAsync([HostPermissions.Read], cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);
        ResourceReference document = Reference(Document);
        ResourceReference report = Reference(Report);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(document, workspace, cancellationToken);
        await deployment.RegisterAsync(report, containedIn: null, cancellationToken);

        // What a first refusal reads once and keeps is read before any is compared.
        _ = await StoredAsync(account, report);
        _ = await StoredAsync(account, Reference(Report));
        _ = await SourcedAsync(account, document);
        _ = await SourcedAsync(account, Reference(Document));

        IReadOnlyList<string> storedPresent = await StoredAsync(account, report);
        IReadOnlyList<string> storedAbsent = await StoredAsync(account, Reference(Report));
        IReadOnlyList<string> sourcedPresent = await SourcedAsync(account, document);
        IReadOnlyList<string> sourcedAbsent = await SourcedAsync(account, Reference(Document));

        Assert.NotEmpty(storedPresent);
        Assert.Equal(storedPresent, storedAbsent);
        Assert.NotEmpty(sourcedPresent);
        Assert.Equal(sourcedPresent, sourcedAbsent);
    }

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, held => held.Id)
            .Relationship("reviewer", reading.Reviewers);

    private static void Refused(Result outcome) =>
        Assert.Equal(ErrorCodes.Denied, outcome.Match(() => ErrorCodes.SystemFault, error => error.Code));

    // The statements of one refusal on a type no derivation reaches.
    private async Task<IReadOnlyList<string>> StoredAsync(SubjectId account, ResourceReference resource)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        using var traced = new TracedStatements();

        Refused(await gate.RequireAsync(
            AccessContext.Of(account),
            HostPermissions.Read,
            resource,
            TestContext.Current.CancellationToken));

        return traced.Texts;
    }

    // The statements of one refusal on a type a derivation reaches, the host's own
    // included.
    private async Task<IReadOnlyList<string>> SourcedAsync(SubjectId account, ResourceReference resource)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        using var traced = new TracedStatements();

        Refused(await gate.RequireAsync(
            AccessContext.Of(account),
            HostPermissions.Read,
            resource,
            Sources(reading),
            TestContext.Current.CancellationToken));

        return traced.Texts;
    }
}
