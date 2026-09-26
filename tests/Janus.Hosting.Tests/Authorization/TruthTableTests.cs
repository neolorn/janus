using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Accounts;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Privacy.Consents;
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
    private static readonly ResourceType Undeclared = ResourceType.Parse("ledger");

    private static readonly Dictionary<ErrorCode, Decided> Refusals = new()
    {
        [ErrorCodes.Denied] = Decided.Denied,
        [ErrorCodes.Restricted] = Decided.Restricted,
        [ErrorCodes.ConsentRequired] = Decided.ConsentRequired,
    };

    // The table itself, stated once. Changing a policy is changing a row here, and both
    // the case-by-case run and the agreement check read it (AUTHZ-TEST-001).
    private static readonly (string Scenario, Decided Decided)[] Table =
    [
        ("a grant on the record itself", Decided.Allowed),
        ("a grant on the container", Decided.Allowed),
        ("a grant two containers above", Decided.Allowed),
        ("a grant on the whole organization", Decided.Allowed),
        ("a grant on a sibling", Decided.Denied),
        ("no grant at all", Decided.Denied),
        ("a grant to a group the account belongs to", Decided.Allowed),
        ("a grant to a group holding the account's group", Decided.Allowed),
        ("a grant to a group the account left", Decided.Denied),
        ("a deny on the record over an allow on the container", Decided.Denied),
        ("a deny on the container over an allow on the record", Decided.Denied),
        ("a deny to a group over an allow to the account", Decided.Denied),
        ("a grant that has expired", Decided.Denied),
        ("a grant that expires later", Decided.Allowed),
        ("a grant that was revoked", Decided.Denied),
        ("a grant in another organization", Decided.Denied),
        ("a grant whose role does not allow the permission", Decided.Denied),
        ("a fact in the host's data conferring a role", Decided.Allowed),
        ("a deny over a fact in the host's data", Decided.Denied),
        ("a fact in the host's data on a container above", Decided.Allowed),
        ("a grant on the container the record was moved into", Decided.Allowed),
        ("a grant on the container the record was moved out of", Decided.Denied),
        ("a record of a type the model does not declare", Decided.Raised),
    ];

    // The decisions an operation's own gate step makes over what no list shows, each
    // run through the one path that makes it, and the capability page's decision on
    // each of its records, which the single check is asked beside it and must match
    // (CONV-DESIGN-002 AC3, AUTHZ-SCOPE-001, IDN-ACCT-007 AC2, AUTHZ-GATE-005 AC1).
    private static readonly (string Scenario, Decided Decided)[] Operations =
    [
        ("a revocation of a grant no row names, by a caller managing grants", Decided.Denied),
        ("a revocation of a grant no row names, by a restricted caller", Decided.Restricted),
        ("a change to a group no row names, by a caller managing groups", Decided.Denied),
        ("a change to a group no row names, by a restricted caller", Decided.Restricted),
        ("a grant on a record no registration names, by a caller managing grants", Decided.Denied),
        ("a change to the account's own settings", Decided.Allowed),
        ("a change to the account's own settings, by a restricted caller", Decided.Restricted),
        ("a page's record whose subject gave the consent its purpose asks", Decided.Allowed),
        ("a page's record whose subject gave no consent to its purpose", Decided.ConsentRequired),
    ];

    /// <summary>
    /// The table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> Cases => Read(Table);

    /// <summary>
    /// The operations' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> OperationCases => Read(Operations);

    /// <summary>
    /// AUTHZ-TEST-001 AC1, AC2, AUTHZ-GATE-002 AC2: every case of the table decides the
    /// way the table says, through the check, the expression and the fragment alike.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AUTHZ_TEST_001_AC2_EveryCaseDecidesTheSameWayThroughBothPathsAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteAsync(scenario);

        Assert.Equal(decided, await ChecksAsync(written));
        Assert.Equal(decided, await ExpressionAdmitsAsync(written));
        Assert.Equal(decided, await FragmentAdmitsAsync(written));
    }

    /// <summary>
    /// AUTHZ-TEST-001 AC1, CONV-DESIGN-002 AC3, IDN-ACCT-007 AC2, AUTHZ-GATE-005 AC1:
    /// every case of the operations' table decides the way the table says, through the
    /// path that decides it.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(OperationCases))]
    public async Task AUTHZ_TEST_001_AC1_EveryOperationCaseDecidesTheWayTheTableSaysAsync(
        string scenario,
        Decided decided) =>
        Assert.Equal(decided, await OperationAsync(scenario));

    /// <summary>
    /// AUTHZ-PRIN-001 AC1: the single check and the list filter are asked the whole
    /// table and agree case for case, whatever the table says the answer is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_PRIN_001_AC1_TheCheckAndTheFilterAgreeOnEveryCaseAsync()
    {
        List<(Decided Check, Decided Expression, Decided Fragment)> decided = [];

        foreach ((string scenario, Decided _) in Table)
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
    /// AUTHZ-GATE-002 AC2: the two renderings of the one rule are asked every case of
    /// the table and answer it alike, neither having been written separately.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_002_AC2_EveryCaseIsEqualAcrossBothRenderingsAsync()
    {
        List<(Decided Expression, Decided Fragment)> rendered = [];

        foreach ((string scenario, Decided _) in Table)
        {
            Case written = await WriteAsync(scenario);

            rendered.Add((
                await ExpressionAdmitsAsync(written),
                await FragmentAdmitsAsync(written)));
        }

        Assert.Equal(Table.Length, rendered.Count);
        Assert.All(rendered, outcome => Assert.Equal(outcome.Expression, outcome.Fragment));
    }

    /// <summary>
    /// AUTHZ-TEST-001 AC3, AUTHZ-DERIVE-005: the same deployment with the derivation
    /// materialised decides every case of the table the same way, through the check,
    /// the expression and the fragment alike.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task AUTHZ_TEST_001_AC3_EveryCaseDecidesTheSameWayMaterialisedAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteAsync(scenario);

        Assert.Equal(decided, await ChecksAsync(written));

        await using ServiceProvider materialised = Materialised();
        await RefreshAsync(materialised, written);

        Assert.Equal(decided, await ChecksAsync(written, materialised));
        Assert.Equal(decided, await ExpressionAdmitsAsync(written, materialised));
        Assert.Equal(decided, await FragmentAdmitsAsync(written, materialised));
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
                "identity_authz_row",
                "id",
                TestContext.Current.CancellationToken));

        Assert.DoesNotContain(written.Record.Id.ToString(), fragment.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(HostPermissions.Read.ToString(), fragment.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            written.Deployment.Organization.Value.ToString(),
            fragment.Text,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("@identity_authz_permissions", fragment.Text, StringComparison.Ordinal);
        Assert.Contains("identity_authz_row.id", fragment.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// AUTHZ-GATE-002 AC3: an alias or a column that is not an identifier is refused
    /// rather than written into the fragment's text.
    /// </summary>
    /// <param name="rowAlias">The alias the caller offered.</param>
    /// <param name="column">The column the caller offered.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [InlineData("row; DROP TABLE identity.grants --", "id")]
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
        Assert.Contains("identity.ancestry", sql, StringComparison.Ordinal);
        Assert.Contains("identity.effective_grants", sql, StringComparison.Ordinal);
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
        HostContext reading,
        IServiceProvider? deployment = null)
    {
        await using AsyncServiceScope scope = (deployment ?? written.Host.Services).CreateAsyncScope();

        return Rendered(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .FilterAsync(
                AccessContext.Of(written.Account),
                HostPermissions.Read,
                written.Record.Type,
                written.Deployment.Organization,
                Sources(reading),
                TestContext.Current.CancellationToken));
    }

    private static TheoryData<string, Decided> Read((string Scenario, Decided Decided)[] table)
    {
        var cases = new TheoryData<string, Decided>();

        foreach ((string scenario, Decided decided) in table)
        {
            cases.Add(scenario, decided);
        }

        return cases;
    }

    // What a refusal decides, by the code it answers; any other code is no decision the
    // tables state, and fails the case that met it.
    private static Decided Refused(Error error) =>
        Refusals.TryGetValue(error.Code, out Decided decided)
            ? decided
            : throw new InvalidOperationException(error.Code.ToString());

    // CONV-ERR-001, AUTHZ-PRIN-003: a type the model does not declare is raised by the
    // gate before anything is read, which is what the case decides; the test's own
    // failures are never read as one.
    private static async Task<Decided> RaisedOrAsync(Func<Task<Decided>> asked)
    {
        try
        {
            return await asked();
        }
        catch (InvalidOperationException raised) when (raised.Message.Contains("is not declared", StringComparison.Ordinal))
        {
            return Decided.Raised;
        }
    }

    // What the host supplies from its own context, the same object every path on a type
    // with a derivation takes (D-161).
    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    private static TRendering Rendered<TRendering>(Result<TRendering> outcome) =>
        outcome.Match(
            rendering => rendering,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private async Task<Decided> ChecksAsync(Case written, IServiceProvider? deployment = null) =>
        await RaisedOrAsync(async () =>
        {
            await using AsyncServiceScope scope = (deployment ?? host.Services).CreateAsyncScope();
            await using HostContext reading = host.Context();

            Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(
                    AccessContext.Of(written.Account),
                    HostPermissions.Read,
                    written.Record,
                    Sources(reading),
                    TestContext.Current.CancellationToken);

            return outcome.Match(() => Decided.Allowed, Refused);
        });

    private async Task<Decided> ExpressionAdmitsAsync(Case written, IServiceProvider? deployment = null) =>
        await RaisedOrAsync(async () =>
        {
            await using HostContext reading = host.Context();

            return await reading.Documents
                .Where(await ExpressionAsync(written, reading, deployment))
                .AnyAsync(
                    document => document.Id == written.Record.Id.ToString(),
                    TestContext.Current.CancellationToken)
                ? Decided.Allowed
                : Decided.Denied;
        });

    private async Task<Decided> FragmentAdmitsAsync(Case written, IServiceProvider? deployment = null) =>
        await RaisedOrAsync(async () =>
        {
            SqlFilter fragment;

            await using (AsyncServiceScope scope = (deployment ?? host.Services).CreateAsyncScope())
            {
                fragment = Rendered(await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                    .FragmentAsync(
                        AccessContext.Of(written.Account),
                        HostPermissions.Read,
                        written.Record.Type,
                        written.Deployment.Organization,
                        "identity_authz_row",
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
                        FROM host.documents AS identity_authz_row
                        WHERE identity_authz_row.id = @record AND {fragment.Text});
                    """),
                arguments,
                cancellationToken: TestContext.Current.CancellationToken))
                ? Decided.Allowed
                : Decided.Denied;
        });

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
        ResourceReference elsewhere = Reference(Workspace);
        ResourceReference record = Reference(
            scenario == "a record of a type the model does not declare" ? Undeclared : Document);
        ResourceReference sibling = Reference(Document);

        await deployment.RegisterAsync(outer, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(inner, outer, cancellationToken);
        await deployment.RegisterAsync(elsewhere, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(sibling, inner, cancellationToken);

        // A type the model does not declare is never registered: the library holds no
        // row of it, and the gate reads none.
        if (record.Type == Document)
        {
            await deployment.RegisterAsync(record, inner, cancellationToken);
        }

        await GrantAsync(deployment, scenario, role, account, record, inner, outer, sibling, elsewhere);
        await ReviewAsync(deployment, scenario, account, inner, outer);
        await MovedAsync(scenario, record, elsewhere);

        return new Case(host, deployment, account, record, sibling, inner, outer);
    }

    // AUTHZ-INHERIT-002: the record is moved by the library, whose rewrite of the
    // ancestry is what the case decides by, after its grants are written by hand as
    // every other case's are.
    private async Task MovedAsync(string scenario, ResourceReference record, ResourceReference elsewhere)
    {
        if (scenario is not ("a grant on the container the record was moved into"
            or "a grant on the container the record was moved out of"))
        {
            return;
        }

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result moved = await scope.ServiceProvider.GetRequiredService<IResources>()
            .MoveAsync(record, elsewhere, TestContext.Current.CancellationToken);

        Assert.True(moved.Match(() => true, _ => false));
    }

    // Each operation case in a deployment of its own, its caller holding the management
    // of grants and of groups across the organization, so that what refuses a row no
    // row names is the row's absence and never the caller's want of a permission.
    private async Task<Decided> OperationAsync(string scenario)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend],
            cancellationToken);
        RoleName managing = await deployment.RoleAsync(
            [Permissions.GrantManage, Permissions.GroupManage],
            cancellationToken);
        SubjectId caller = await deployment.AccountAsync(cancellationToken);

        await deployment.GrantAsync(
            GrantSubject.Of(caller), managing, null, false, null, null, cancellationToken);

        if (scenario.EndsWith("by a restricted caller", StringComparison.Ordinal))
        {
            await deployment.RestrictAsync(caller, cancellationToken);
        }

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        var context = AccessContext.Of(caller);
        var session = SessionId.New(TimeProvider.System);

        switch (scenario)
        {
            case "a revocation of a grant no row names, by a caller managing grants":
            case "a revocation of a grant no row names, by a restricted caller":
                return (await scope.ServiceProvider.GetRequiredService<IGrants>()
                        .RevokeAsync(
                            context,
                            session,
                            GrantId.New(TimeProvider.System),
                            "No longer needed.",
                            cancellationToken))
                    .Match(() => Decided.Allowed, Refused);

            case "a change to a group no row names, by a caller managing groups":
            case "a change to a group no row names, by a restricted caller":
                return (await scope.ServiceProvider.GetRequiredService<IGroups>()
                        .RemoveAsync(context, GroupId.New(TimeProvider.System), "No longer used.", cancellationToken))
                    .Match(() => Decided.Allowed, Refused);

            case "a grant on a record no registration names, by a caller managing grants":
                return (await scope.ServiceProvider.GetRequiredService<IGrants>()
                        .GrantAsync(
                            context,
                            session,
                            new GrantRequest(
                                GrantSubject.Of(caller),
                                role,
                                Reference(Document),
                                Deny: false,
                                ExpiresAt: null,
                                "The reason the grant was asked for."),
                            cancellationToken))
                    .Match(_ => Decided.Allowed, Refused);

            case "a change to the account's own settings":
            case "a change to the account's own settings, by a restricted caller":
                return await scope.ServiceProvider.GetRequiredService<ISettingsRestriction>()
                    .RefusedAsync(caller, cancellationToken) is Error refused
                    ? Refused(refused)
                    : Decided.Allowed;

            case "a page's record whose subject gave the consent its purpose asks":
            case "a page's record whose subject gave no consent to its purpose":
                return await PagedAsync(
                    deployment,
                    role,
                    caller,
                    consented: scenario == "a page's record whose subject gave the consent its purpose asks");

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "No such case.");
        }
    }

    // AUTHZ-GATE-005 AC1, PRIV-SENS-002 AC1: one page holding a record of a subject who
    // consented and one of a subject who did not, the caller granted on the workspace
    // both sit in; the case's record is decided by the page and by the single check,
    // which must agree.
    private async Task<Decided> PagedAsync(
        Deployment deployment,
        RoleName role,
        SubjectId caller,
        bool consented)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ResourceReference workspace = Reference(Workspace);
        ResourceReference given = Reference(Document);
        ResourceReference withheld = Reference(Document);
        SubjectId giving = await deployment.AccountAsync(cancellationToken);
        SubjectId withholding = await deployment.AccountAsync(cancellationToken);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(given, workspace, cancellationToken, giving);
        await deployment.RegisterAsync(withheld, workspace, cancellationToken, withholding);
        await deployment.GrantAsync(
            GrantSubject.Of(caller), role, workspace, false, null, null, cancellationToken);
        await ConsentedAsync(giving);

        ResourceReference asked = consented ? given : withheld;

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        Capability paged = Rendered(await gate.CapabilitiesAsync(
                AccessContext.Of(caller),
                Document,
                [given.Id, withheld.Id],
                [HostPermissions.Recommend],
                Sources(reading),
                cancellationToken))
            .Single(capability => capability.Resource == asked.Id);

        Decided checkedAlone = (await gate.RequireAsync(
                AccessContext.Of(caller),
                HostPermissions.Recommend,
                asked,
                Sources(reading),
                cancellationToken))
            .Match(() => Decided.Allowed, Refused);

        Assert.Equal(checkedAlone, Paged(paged));

        return checkedAlone;
    }

    // What the page decides on one record: the consent it still requires, the action
    // it admits, or neither.
    private static Decided Paged(Capability capability) =>
        capability.Requires.TryGetValue(HostPermissions.Recommend, out IReadOnlySet<CapabilityResidual>? outstanding)
            && outstanding.SetEquals([CapabilityResidual.Consent])
            ? Decided.ConsentRequired
            : capability.Can.Contains(HostPermissions.Recommend) ? Decided.Allowed : Decided.Denied;

    // PRIV-SENS-002 AC1: the written consent the consent-based purpose asks of a
    // sensitive type, recorded for its data subject.
    private async Task ConsentedAsync(SubjectId subject)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<IConsentStore>().RecordAsync(
            subject,
            new ConsentRecord(
                "recommendations",
                "1",
                ConsentMechanism.Dashboard,
                ConsentKind.Written,
                Deployment.Noon,
                WithdrawnAt: null,
                SupersededAt: null),
            cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    private async Task GrantAsync(
        Deployment deployment,
        string scenario,
        RoleName role,
        SubjectId account,
        ResourceReference record,
        ResourceReference inner,
        ResourceReference outer,
        ResourceReference sibling,
        ResourceReference elsewhere)
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

            case "a fact in the host's data conferring a role":
            case "a fact in the host's data on a container above":
                break;

            case "a deny over a fact in the host's data":
                await deployment.GrantAsync(
                    holder, role, record, true, null, null, cancellationToken);
                break;

            case "a grant on the container the record was moved into":
                await deployment.GrantAsync(
                    holder, role, elsewhere, false, null, null, cancellationToken);
                break;

            case "a grant on the container the record was moved out of":
                await deployment.GrantAsync(
                    holder, role, inner, false, null, null, cancellationToken);
                break;

            // The whole organization is granted, so a type the gate answered rather than
            // raised would be allowed.
            case "a record of a type the model does not declare":
                await deployment.GrantAsync(
                    holder, role, null, false, null, null, cancellationToken);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "No such case.");
        }
    }

    // AUTHZ-DERIVE-001, AUTHZ-DERIVE-002: the fact is the host's own row, and the role
    // it confers is read where the derivation is evaluated, so the role exists only for
    // the cases that follow from one.
    private static async Task ReviewAsync(
        Deployment deployment,
        string scenario,
        SubjectId account,
        ResourceReference inner,
        ResourceReference outer)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        ResourceReference? reviewed = scenario switch
        {
            "a fact in the host's data conferring a role" => inner,
            "a deny over a fact in the host's data" => inner,
            "a fact in the host's data on a container above" => outer,
            _ => null,
        };

        if (reviewed is not ResourceReference workspace)
        {
            return;
        }

        await deployment.NamedRoleAsync(
            RoleName.Parse("reviewer"),
            [HostPermissions.Read],
            cancellationToken);

        await deployment.ReviewAsync(workspace, account, cancellationToken);
    }

    // The same deployment with the one derivation precomputed into grant rows, which
    // is what AUTHZ-TEST-001 AC3 asks the table of a second time.
    private ServiceProvider Materialised()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddJanus(
            host.ConnectionString,
            new KeyEncryptionKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new FingerprintKeys(1, new Dictionary<int, ReadOnlyMemory<byte>> { [1] = new byte[32] }),
            new byte[16],
            Encoding.UTF8.GetBytes(host.MaintenanceConnectionString),
            HostFixture.Declaration(materialised: true),
            ApplicationKind.Public);

        return services.BuildServiceProvider();
    }

    // AUTHZ-DERIVE-005: the host refreshes the derivation from the operation that
    // changed the relationship, inside its own unit of work. The case writes its facts
    // on one of the two workspaces, and a refresh of the other writes nothing.
    private async Task RefreshAsync(IServiceProvider deployment, Case written)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = deployment.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IDerivationMaterialiser materialiser =
            scope.ServiceProvider.GetRequiredService<IDerivationMaterialiser>();

        await work.BeginAsync(cancellationToken);

        foreach (ResourceReference workspace in new[] { written.Outer, written.Inner })
        {
            Rendered(await materialiser.RefreshAsync(
                AccessContext.Of(written.Deployment.Granter),
                "reviewer",
                workspace.Id,
                Sources(reading),
                cancellationToken));
        }

        await work.CommitAsync(cancellationToken);
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
            UPDATE identity.grants
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
        ResourceReference Sibling,
        ResourceReference Inner,
        ResourceReference Outer);
}
