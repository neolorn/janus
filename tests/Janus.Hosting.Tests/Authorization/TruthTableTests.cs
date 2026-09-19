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
/// The truth table: every relationship, permission and condition that decides an
/// outcome, run through the single check and through both renderings of the filter
/// (AUTHZ-TEST-001, AUTHZ-PRIN-001, AUTHZ-GATE-002).
/// </summary>
/// <remarks>
/// A case that disagrees across the three is the failure this table exists to catch:
/// a list screen showing what a check would refuse is a silent leak rather than a
/// crash. The expected outcome is stated here first and the code is made to match it.
/// </remarks>
[Trait("kind", "integration")]
public sealed class TruthTableTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    // The table itself, stated once. Changing a policy is changing a row here, and both
    // the case-by-case run and the agreement check read it (AUTHZ-TEST-001).
    private static readonly (string Scenario, bool Allowed)[] Table =
    [
        ("a grant on the record itself", true),
        ("a grant on the container", true),
        ("a grant two containers above", true),
        ("a grant on the whole organization", true),
        ("a grant on a sibling", false),
        ("no grant at all", false),
        ("a grant to a group the account belongs to", true),
        ("a grant to a group holding the account's group", true),
        ("a grant to a group the account left", false),
        ("a deny on the record over an allow on the container", false),
        ("a deny on the container over an allow on the record", false),
        ("a deny to a group over an allow to the account", false),
        ("a grant that has expired", false),
        ("a grant that expires later", true),
        ("a grant that was revoked", false),
        ("a grant in another organization", false),
        ("a grant whose role does not allow the permission", false),
    ];

    /// <summary>
    /// The table as the run reads it.
    /// </summary>
    public static TheoryData<string, bool> Cases
    {
        get
        {
            var cases = new TheoryData<string, bool>();

            foreach ((string scenario, bool allowed) in Table)
            {
                cases.Add(scenario, allowed);
            }

            return cases;
        }
    }

    /// <summary>
    /// AUTHZ-TEST-001 AC1, AC2, AUTHZ-GATE-002 AC2: every case of the table decides the
    /// way the table says, through the check, the expression and the fragment alike.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="allowed">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync(
        string scenario,
        bool allowed)
    {
        Case written = await WriteAsync(scenario);

        Assert.Equal(allowed, await ChecksAsync(written));
        Assert.Equal(allowed, await ExpressionAdmitsAsync(written));
        Assert.Equal(allowed, await FragmentAdmitsAsync(written));
    }

    /// <summary>
    /// AUTHZ-PRIN-001 AC1: the single check and the list filter are asked the whole
    /// table and agree case for case, whatever the table says the answer is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync()
    {
        List<(bool Check, bool Expression, bool Fragment)> decided = [];

        foreach ((string scenario, bool _) in Table)
        {
            Case written = await WriteAsync(scenario);

            decided.Add((
                await ChecksAsync(written),
                await ExpressionAdmitsAsync(written),
                await FragmentAdmitsAsync(written)));
        }

        Assert.Equal(Table.Length, decided.Count);
        Assert.All(decided, outcome => Assert.Equal(outcome.Check, outcome.Expression));
        Assert.All(decided, outcome => Assert.Equal(outcome.Check, outcome.Fragment));
    }

    /// <summary>
    /// AUTHZ-GATE-002 AC3: the fragment carries every value as a parameter, so nothing
    /// a caller supplied reaches its text.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GATE_002_AC3_TheFragmentInterpolatesNoValueAsync()
    {
        Case written = await WriteAsync("a grant on the record itself");

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        SqlFilter fragment = Rendered(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .FragmentAsync(
                AccessContext.Of(written.Account),
                HostPermissions.Read,
                Document,
                written.Deployment.Organization,
                "janus_authz_row",
                "id",
                TestContext.Current.CancellationToken));

        Assert.DoesNotContain(written.Record.Id.ToString(), fragment.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(HostPermissions.Read.ToString(), fragment.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            written.Deployment.Organization.Value.ToString(),
            fragment.Text,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("@janus_authz_permissions", fragment.Text, StringComparison.Ordinal);
        Assert.Contains("janus_authz_row.id", fragment.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTHZ-GATE-002 AC3: an alias or a column that is not an identifier is refused
    /// rather than written into the fragment's text.
    /// </summary>
    /// <param name="rowAlias">The alias the caller offered.</param>
    /// <param name="column">The column the caller offered.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("row; DROP TABLE janus.grants --", "id")]
    [InlineData("row", "id) OR (1=1")]
    [InlineData("Row", "id")]
    [InlineData("1row", "id")]
    public async Task AUTHZ_GATE_002_AC3_AnAliasThatIsNotAnIdentifierIsRefusedAsync(
        string rowAlias,
        string column)
    {
        Case written = await WriteAsync("no grant at all");

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        await Assert.ThrowsAsync<ArgumentException>(async () => await gate.FragmentAsync(
            AccessContext.Of(written.Account),
            HostPermissions.Read,
            Document,
            written.Deployment.Organization,
            rowAlias,
            column,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTHZ-PRIN-002 AC1, AC2: the predicate goes to the database as a correlated
    /// existence check over the two contract tables, so the host's listing stays one
    /// query and nothing is filtered after retrieval.
    /// </summary>
    [Fact]
    public async Task AUTHZ_PRIN_002_AC1_TheExpressionTranslatesToOneCorrelatedQueryAsync()
    {
        Case written = await WriteAsync("a grant on the container");

        await using HostContext reading = host.Context();

        IQueryable<HostDocument> listing = reading.Documents
            .Where(await ExpressionAsync(written, reading));

        string sql = listing.ToQueryString();

        Assert.Contains("EXISTS (", sql, StringComparison.Ordinal);
        Assert.Contains("janus.ancestry", sql, StringComparison.Ordinal);
        Assert.Contains("janus.effective_grants", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("RECURSIVE", sql, StringComparison.OrdinalIgnoreCase);

        // The grant sits on the container, so both records beneath it come back, and
        // the database returned exactly the rows that were displayed.
        Assert.Equal(
            new[] { written.Record.Id.ToString(), written.Sibling.Id.ToString() }
                .OrderBy(id => id, StringComparer.Ordinal),
            (await listing.Select(document => document.Id)
                .ToListAsync(TestContext.Current.CancellationToken))
                .OrderBy(id => id, StringComparer.Ordinal));
    }

    /// <summary>
    /// LIB-HOST-002 AC2: the filter composes into the host's own query, over the host's
    /// own table and the sets the host supplied, and the rows are counted in the
    /// database rather than brought back to be counted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_002_AC2_TheFilterComposesWithoutMaterialisingRowsAsync()
    {
        Case written = await WriteAsync("a grant on the container");

        await using HostContext reading = host.Context();

        IQueryable<HostDocument> listing = reading.Documents
            .Where(await ExpressionAsync(written, reading))
            .OrderBy(document => document.Id)
            .Skip(1)
            .Take(1);

        string sql = listing.ToQueryString();

        Assert.Contains("host.documents", sql, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
        Assert.Contains("OFFSET", sql, StringComparison.Ordinal);

        Assert.Equal(
            2,
            await reading.Documents
                .Where(await ExpressionAsync(written, reading))
                .CountAsync(TestContext.Current.CancellationToken));

        Assert.Single(await listing.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<Expression<Func<HostDocument, bool>>> ExpressionAsync(
        Case written,
        HostContext reading)
    {
        await using AsyncServiceScope scope = written.Host.Services.CreateAsyncScope();

        return Rendered(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .FilterAsync(
                AccessContext.Of(written.Account),
                HostPermissions.Read,
                Document,
                written.Deployment.Organization,
                new FilterSources<HostDocument>(
                    reading.Ancestry,
                    reading.Grants,
                    document => document.Id),
                TestContext.Current.CancellationToken));
    }

    private static TRendering Rendered<TRendering>(Result<TRendering> outcome) =>
        outcome.Match(
            rendering => rendering,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private async Task<bool> ChecksAsync(Case written)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(written.Account),
                HostPermissions.Read,
                written.Record,
                TestContext.Current.CancellationToken);

        return outcome.Match(() => true, _ => false);
    }

    private async Task<bool> ExpressionAdmitsAsync(Case written)
    {
        await using HostContext reading = host.Context();

        return await reading.Documents
            .Where(await ExpressionAsync(written, reading))
            .AnyAsync(
                document => document.Id == written.Record.Id.ToString(),
                TestContext.Current.CancellationToken);
    }

    private async Task<bool> FragmentAdmitsAsync(Case written)
    {
        SqlFilter fragment;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            fragment = Rendered(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .FragmentAsync(
                    AccessContext.Of(written.Account),
                    HostPermissions.Read,
                    Document,
                    written.Deployment.Organization,
                    "janus_authz_row",
                    "id",
                    TestContext.Current.CancellationToken));
        }

        var arguments = new DynamicParameters();

        foreach (KeyValuePair<string, object> parameter in fragment.Parameters)
        {
            arguments.Add(parameter.Key, parameter.Value);
        }

        arguments.Add("record", written.Record.Id.ToString());

        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            string.Create(
                CultureInfo.InvariantCulture,
                $"""
                SELECT EXISTS (
                    SELECT 1
                    FROM host.documents AS janus_authz_row
                    WHERE janus_authz_row.id = @record AND {fragment.Text});
                """),
            arguments,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private async Task<Case> WriteAsync(string scenario)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            scenario == "a grant whose role does not allow the permission"
                ? [HostPermissions.Edit]
                : [HostPermissions.Read],
            cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        ResourceReference outer = Reference(Workspace);
        ResourceReference inner = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference sibling = Reference(Document);

        await deployment.RegisterAsync(outer, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(inner, outer, cancellationToken);
        await deployment.RegisterAsync(record, inner, cancellationToken);
        await deployment.RegisterAsync(sibling, inner, cancellationToken);

        await GrantAsync(deployment, scenario, role, account, record, inner, outer, sibling);

        return new Case(host, deployment, account, record, sibling);
    }

    private async Task GrantAsync(
        Deployment deployment,
        string scenario,
        RoleName role,
        SubjectId account,
        ResourceReference record,
        ResourceReference inner,
        ResourceReference outer,
        ResourceReference sibling)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var holder = GrantSubject.Of(account);

        switch (scenario)
        {
            case "a grant on the record itself":
            case "a grant whose role does not allow the permission":
                await deployment.GrantAsync(
                    holder, role, record, false, null, null, cancellationToken);
                break;

            case "a grant on the container":
                await deployment.GrantAsync(
                    holder, role, inner, false, null, null, cancellationToken);
                break;

            case "a grant two containers above":
                await deployment.GrantAsync(
                    holder, role, outer, false, null, null, cancellationToken);
                break;

            case "a grant on the whole organization":
                await deployment.GrantAsync(
                    holder, role, null, false, null, null, cancellationToken);
                break;

            case "a grant on a sibling":
                await deployment.GrantAsync(
                    holder, role, sibling, false, null, null, cancellationToken);
                break;

            case "no grant at all":
                break;

            case "a grant to a group the account belongs to":
                await JoinedAsync(deployment, role, account, record, nested: false, holds: true);
                break;

            case "a grant to a group holding the account's group":
                await JoinedAsync(deployment, role, account, record, nested: true, holds: true);
                break;

            case "a grant to a group the account left":
                await JoinedAsync(deployment, role, account, record, nested: false, holds: false);
                break;

            case "a deny on the record over an allow on the container":
                await deployment.GrantAsync(
                    holder, role, inner, false, null, null, cancellationToken);
                await deployment.GrantAsync(
                    holder, role, record, true, null, null, cancellationToken);
                break;

            case "a deny on the container over an allow on the record":
                await deployment.GrantAsync(
                    holder, role, record, false, null, null, cancellationToken);
                await deployment.GrantAsync(
                    holder, role, inner, true, null, null, cancellationToken);
                break;

            case "a deny to a group over an allow to the account":
                await deployment.GrantAsync(
                    holder, role, record, false, null, null, cancellationToken);
                await DeniedThroughGroupAsync(deployment, role, account, record);
                break;

            case "a grant that has expired":
                await deployment.GrantAsync(
                    holder, role, record, false, Deployment.Noon.AddHours(-1), null, cancellationToken);
                break;

            case "a grant that expires later":
                await deployment.GrantAsync(
                    holder, role, record, false, Deployment.Noon.AddHours(1), null, cancellationToken);
                break;

            case "a grant that was revoked":
                await RevokedAsync(deployment, role, account, record);
                break;

            case "a grant in another organization":
                await deployment.GrantAsync(
                    holder, role, record, false, null, await ElsewhereAsync(), cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "No such case.");
        }
    }

    private async Task<OrganizationId> ElsewhereAsync()
    {
        var elsewhere = new Deployment(host);
        await elsewhere.BeginAsync([HostPermissions.Read], TestContext.Current.CancellationToken);

        return elsewhere.Organization;
    }

    private static async Task JoinedAsync(
        Deployment deployment,
        RoleName role,
        SubjectId account,
        ResourceReference record,
        bool nested,
        bool holds)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        GroupId team = await deployment.GroupAsync(cancellationToken);

        if (holds)
        {
            await deployment.JoinAsync(team, GrantSubject.Of(account), cancellationToken);
        }

        GroupId granted = team;

        if (nested)
        {
            granted = await deployment.GroupAsync(cancellationToken);
            await deployment.JoinAsync(granted, GrantSubject.Of(team), cancellationToken);
        }

        await deployment.GrantAsync(
            GrantSubject.Of(granted), role, record, false, null, null, cancellationToken);
    }

    private static async Task DeniedThroughGroupAsync(
        Deployment deployment,
        RoleName role,
        SubjectId account,
        ResourceReference record)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        GroupId team = await deployment.GroupAsync(cancellationToken);

        await deployment.JoinAsync(team, GrantSubject.Of(account), cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(team), role, record, true, null, null, cancellationToken);
    }

    private async Task RevokedAsync(
        Deployment deployment,
        RoleName role,
        SubjectId account,
        ResourceReference record)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        GrantId grant = await deployment.GrantAsync(
            GrantSubject.Of(account), role, record, false, null, null, cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE janus.grants
            SET revoked_by = subject_id, revoked_at = @at, revocation_reason = @reason
            WHERE id = @id;
            """,
            new
            {
                id = grant.Value,
                at = Deployment.Noon.AddMinutes(-1),
                reason = "The reason the grant was taken back.",
            },
            cancellationToken: cancellationToken));
    }

    private sealed record Case(
        HostFixture Host,
        Deployment Deployment,
        SubjectId Account,
        ResourceReference Record,
        ResourceReference Sibling);
}
