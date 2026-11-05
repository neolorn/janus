using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Hosting.Bff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// An organization-wide grant reaches only the rows the library registers as its own
/// organization's, through the single check, the list filter, the SQL fragment and the
/// capability page alike (AUTHZ-SCOPE-001, AUTHZ-PRIN-001, D-265).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class OrganizationWideGrantTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");

    /// <summary>
    /// AUTHZ-SCOPE-001 AC5 (D-265): over a list holding a record of the grant's
    /// organization, one a second organization owns and a row the library holds no record
    /// for, the list filter and the SQL fragment admit the first alone, as the single
    /// check does.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AC5_AListAdmitsOnlyTheRowsTheGrantsOrganizationOwnsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WrittenAsync();
        string[] listed = [.. written.Listed.Select(record => record.Id.ToString())];

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        Expression<Func<HostDocument, bool>> filter = Rendered(await gate.FilterAsync(
            AccessContext.Of(written.Account),
            HostPermissions.Read,
            Document,
            written.Own.Organization,
            Sources(reading),
            cancellationToken));

        List<string> expressed = await reading.Documents
            .Where(filter)
            .Where(document => listed.Contains(document.Id))
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);

        SqlFilter fragment = Rendered(await gate.FragmentAsync(
            AccessContext.Of(written.Account),
            HostPermissions.Read,
            Document,
            written.Own.Organization,
            "identity_authz_row",
            "id",
            cancellationToken));

        var arguments = new DynamicParameters();

        foreach (KeyValuePair<string, object> parameter in fragment.Parameters)
        {
            arguments.Add(parameter.Key, parameter.Value);
        }

        arguments.Add("listed", listed);

        await using NpgsqlConnection connection = await host.OpenAsync();

        IEnumerable<string> fragmented = await connection.QueryAsync<string>(new CommandDefinition(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT identity_authz_row.id
                FROM host.documents AS identity_authz_row
                WHERE identity_authz_row.id = ANY(@listed) AND {fragment.Text};
                """),
            arguments,
            cancellationToken: cancellationToken));

        bool[] checkedEach = await Task.WhenAll(written.Listed.Select(record => ChecksAsync(written.Account, record)));

        Assert.Equal([written.Ours.Id.ToString()], expressed);
        Assert.Equal([written.Ours.Id.ToString()], fragmented);
        Assert.Equal([true, false, false], checkedEach);
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 AC5 (D-265): a page whose first record a second organization owns,
    /// holding a record of the first organization and a row the library holds no record
    /// for, answers each record by the grants of the organization that owns it, and the
    /// row nothing, over the host's rows and over the library's own alike.
    /// </summary>
    /// <param name="sourced">Whether the page is asked over the host's rows.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AUTHZ_SCOPE_001_AC5_APageAnswersEachRecordInTheOrganizationOwningItAsync(bool sourced)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WrittenAsync();

        // The page leads with the second organization's record, whose own grant allows
        // editing and not reading.
        ResourceId[] page = [written.Theirs.Id, written.Ours.Id, written.Unheld.Id];

        await using ServiceProvider deployed = Deployed(materialised: !sourced);
        await using AsyncServiceScope scope = deployed.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();
        Permission[] asked = [HostPermissions.Read, HostPermissions.Edit];

        IReadOnlyList<Capability> answered = Rendered(sourced
            ? await gate.CapabilitiesAsync(AccessContext.Of(written.Account), Document, page, asked, Sources(reading), cancellationToken)
            : await gate.CapabilitiesAsync(AccessContext.Of(written.Account), Document, page, asked, cancellationToken));

        Assert.Equal(page, answered.Select(capability => capability.Resource));
        Assert.Equal(
            [[HostPermissions.Edit.ToString()], [HostPermissions.Read.ToString()], []],
            answered.Select(capability => capability.Can.Select(permission => permission.ToString()).Order(StringComparer.Ordinal).ToArray()));
    }

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    private static T Rendered<T>(Result<T> outcome) =>
        outcome.Match(
            rendering => rendering,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ResourceReference Reference() =>
        new(Document, ResourceId.Parse(Guid.NewGuid().ToString()));

    // The deployment over the fixture's database, its derivation evaluated from the
    // host's rows or materialised, which is the declaration a page over the library's own
    // rows is asked under (AUTHZ-DERIVE-005).
    private ServiceProvider Deployed(bool materialised)
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        HostFixture.Sourced(services, host.ConnectionString);
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(materialised), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // One account holding an organization-wide grant of reading in its organization and
    // one of editing in a second; a record of each, and a row of the host's that the
    // library holds no record for.
    private async Task<Written> WrittenAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var own = new Deployment(host);
        var other = new Deployment(host);

        RoleName reading = await own.BeginAsync([HostPermissions.Read], cancellationToken);
        RoleName editing = await other.BeginAsync([HostPermissions.Edit], cancellationToken);
        SubjectId account = await own.AccountAsync(cancellationToken);
        ResourceReference ours = Reference();
        ResourceReference theirs = Reference();
        ResourceReference unheld = Reference();

        await own.RegisterAsync(ours, containedIn: null, cancellationToken);
        await other.RegisterAsync(theirs, containedIn: null, cancellationToken);
        await own.GrantAsync(GrantSubject.Of(account), reading, null, false, null, null, cancellationToken);
        await other.GrantAsync(GrantSubject.Of(account), editing, null, false, null, null, cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO host.documents (id, title) VALUES (@id, @title);",
            new { id = unheld.Id.ToString(), title = "A record the host never registered." },
            cancellationToken: cancellationToken));

        return new Written(own, account, ours, theirs, unheld);
    }

    private async Task<bool> ChecksAsync(SubjectId account, ResourceReference record)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(AccessContext.Of(account), HostPermissions.Read, record, Sources(reading), TestContext.Current.CancellationToken))
            .Match(() => true, _ => false);
    }

    private sealed record Written(
        Deployment Own,
        SubjectId Account,
        ResourceReference Ours,
        ResourceReference Theirs,
        ResourceReference Unheld)
    {
        public ResourceReference[] Listed => [Ours, Theirs, Unheld];
    }
}
