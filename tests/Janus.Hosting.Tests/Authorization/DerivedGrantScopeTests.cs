using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// A derived grant reaches only the rows the library registers in the organization it is
/// asked in, through the single check, the list filter, the SQL fragment and the
/// capability page alike, as a stored one does (AUTHZ-SCOPE-001, AUTHZ-PRIN-001, D-266).
/// </summary>
/// <param name="host">The deployment the cases run against.</param>
[Trait("kind", "integration")]
public sealed class DerivedGrantScopeTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    private static readonly RoleName Reviewer = RoleName.Parse("reviewer");

    /// <summary>
    /// AUTHZ-SCOPE-001 AC6 (D-266): the account reviews a workspace of its organization
    /// and one of a second; the list filter and the SQL fragment asked for the first admit
    /// its own record alone, whether the second is live or suspended, and where the
    /// second is suspended the single check refuses that record too.
    /// </summary>
    /// <param name="suspended">Whether the second organization's deletion was requested.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUTHZ_SCOPE_001_AC6_AListAdmitsNoRowADerivationReachesInAnotherOrganizationAsync(bool suspended)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WrittenAsync(administrative: false);

        if (suspended)
        {
            await written.Other.SuspendAsync(cancellationToken);
        }

        string[] listed = [written.Ours.Id.ToString(), written.Theirs.Id.ToString()];

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

        bool ours = await ChecksAsync(written.Account, written.Ours);
        bool theirs = await ChecksAsync(written.Account, written.Theirs);

        Assert.Equal([written.Ours.Id.ToString()], expressed);
        Assert.Equal([written.Ours.Id.ToString()], fragmented);
        Assert.True(ours);
        Assert.Equal(!suspended, theirs);
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 AC6 (D-266): a page led by a record of a second organization judges
    /// each record's derivation in the organization owning it: the second organization's
    /// record is answered by its derivation while that organization is live, and nothing
    /// where it is suspended or is the administrative organization the account holds no
    /// membership of, as the single check answers it.
    /// </summary>
    /// <param name="other">What the second organization is.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("live")]
    [InlineData("suspended")]
    [InlineData("administrative")]
    public async Task AUTHZ_SCOPE_001_AC6_APageJudgesEachDerivationInTheOrganizationOwningItsRecordAsync(string other)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Written written = await WrittenAsync(administrative: other is "administrative");

        if (other is "suspended")
        {
            await written.Other.SuspendAsync(cancellationToken);
        }

        ResourceId[] page = [written.Theirs.Id, written.Ours.Id];

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IReadOnlyList<Capability> answered = Rendered(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .CapabilitiesAsync(
                AccessContext.Of(written.Account),
                Document,
                page,
                [HostPermissions.Read],
                Sources(reading),
                cancellationToken));

        bool theirs = await ChecksAsync(written.Account, written.Theirs);

        Assert.Equal(page, answered.Select(capability => capability.Resource));
        Assert.Equal(
            [other is "live", true],
            answered.Select(capability => capability.Can.Contains(HostPermissions.Read)));
        Assert.Equal(other is "live", theirs);
    }

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    private static T Rendered<T>(Result<T> outcome) =>
        outcome.Match(
            rendering => rendering,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    // One account reviewing, in the host's own data, a workspace of its organization and
    // one of a second, the administrative organization where asked, which it holds no
    // membership of; a record in each workspace, and the role the derivation confers
    // allowing reading.
    private async Task<Written> WrittenAsync(bool administrative)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var own = new Deployment(host);
        var other = new Deployment(host);

        _ = await own.BeginAsync([HostPermissions.Read], cancellationToken);
        _ = await other.BeginAsync([HostPermissions.Read], cancellationToken);
        _ = await own.NamedRoleAsync(Reviewer, [HostPermissions.Read], cancellationToken);

        OrganizationId? elsewhere = administrative ? await other.AdministrativeAsync(cancellationToken) : null;
        SubjectId account = await own.AccountAsync(cancellationToken);
        ResourceReference ourWorkspace = Reference(Workspace);
        ResourceReference theirWorkspace = Reference(Workspace);
        ResourceReference ours = Reference(Document);
        ResourceReference theirs = Reference(Document);

        await own.RegisterAsync(ourWorkspace, containedIn: null, cancellationToken);
        await own.RegisterAsync(ours, ourWorkspace, cancellationToken);
        await other.RegisterAsync(theirWorkspace, containedIn: null, cancellationToken, organization: elsewhere);
        await other.RegisterAsync(theirs, theirWorkspace, cancellationToken, organization: elsewhere);
        await own.ReviewAsync(ourWorkspace, account, cancellationToken);
        await own.ReviewAsync(theirWorkspace, account, cancellationToken);

        return new Written(own, other, account, ours, theirs);
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
        Deployment Other,
        SubjectId Account,
        ResourceReference Ours,
        ResourceReference Theirs);
}
