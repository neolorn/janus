using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Accounts;
using Janus.Authentication.Factors;
using Janus.Authentication.Sessions;
using Janus.Authorization.Gate;
using Janus.Authorization.Tests.Gate;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Background;
using Janus.Hosting.Bff;
using Janus.Privacy.Consents;
using Janus.Privacy.SubjectKeys;
using Janus.Storage;
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
        [ErrorCodes.ConsentSuperseded] = Decided.ConsentSuperseded,
        [ErrorCodes.ConsentWrittenRequired] = Decided.ConsentWrittenRequired,
        [ErrorCodes.StepUpRequired] = Decided.StepUpRequired,
        [ErrorCodes.StepUpUnavailable] = Decided.StepUpUnavailable,
    };

    // The gate a report is judged against: the account is a member of the case's
    // organization, whose policy states one gate at two factors, phishing-resistant,
    // five minutes old at most, so the gate the host names costs that
    // (AUTHZ-GATE-005, AUTH-STEP-002).
    private static readonly Gate Strict = new(GateLevel.Aal2, PhishingResistant: true, TimeSpan.FromMinutes(5));

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
        ("a permission the model does not declare", Decided.Raised),
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
        ("a lookup of a record no registration names, by a caller reading grants", Decided.Denied),
        ("a change to the account's own settings", Decided.Allowed),
        ("a change to the account's own settings, by a restricted caller", Decided.Restricted),
        ("a page's record whose subject gave the consent its purpose asks", Decided.Allowed),
        ("a page's record whose subject gave no consent to its purpose", Decided.ConsentRequired),
        ("a page's record under a bound action, on a session that proved its gate", Decided.Allowed),
        ("a page's record under a bound action, on a session whose proof has aged", Decided.StepUpRequired),
        (
            "a page's record under a bound action, on a session downgraded since it proved its gate",
            Decided.ReauthenticationRequired),
        ("a grant in the administrative organization, to a member of it", Decided.Allowed),
        ("a grant in the administrative organization, to an account holding no membership of it", Decided.Denied),
        ("a check by background work, which holds no grant", Decided.Denied),
        ("a check refused inside work the caller rolls back", Decided.Denied),
        ("a modifying check asked again inside the caller's unit of work", Decided.Allowed),
        (
            "a modifying check asked again inside the caller's unit of work, by a caller restricted since the gate step",
            Decided.Restricted),
        (
            "a settings change asked again inside its unit of work, by a caller restricted since the gate step",
            Decided.Restricted),
        ("a group created, by a caller managing groups", Decided.Allowed),
        ("a group created, by a caller restricted since the gate step", Decided.Restricted),
        ("a member a group already holds added again, by a caller managing groups", Decided.Allowed),
        ("a member a group does not hold taken out, by a caller managing groups", Decided.Allowed),
        ("a lookup of a record a fact in the host's data reaches, by a caller reading grants", Decided.Allowed),
        ("a fact in the host's data no grant was materialised for, after the drift check", Decided.Allowed),
        ("a materialised grant the host's data no longer supports, after the drift check", Decided.Denied),
    ];

    // An action bound to a step-up gate, judged from what a host's assurance provider
    // reports of the caller where no session of the library carries the request: every
    // outcome of the report against the gate (AUTHZ-TEST-001 AC1, LIB-HOST-004,
    // AUTH-STEP-002, AUTH-STEP-003). The gate is one the host names, costing the
    // strictest of the gates of the policy the caller is under. The caller's grant admits
    // the record in every case, so what decides is the gate, and a list asked under the
    // action is refused with the code the check answers (AUTHZ-TEST-001 AC2,
    // AUTHZ-GATE-005).
    private static readonly (string Scenario, Decided Decided)[] StepUps =
    [
        ("a report that meets the gate", Decided.Allowed),
        ("a report below the level the gate asks", Decided.StepUpRequired),
        ("a report that last reached the gate's level before its maximum age, and a lower one within it", Decided.StepUpRequired),
        ("a report that last reached a level above the gate's within its maximum age", Decided.Allowed),
        ("a report that was not phishing-resistant, at a gate asking it", Decided.StepUpRequired),
        ("a report that last reached phishing resistance before the gate's maximum age", Decided.StepUpRequired),
        ("a report older than the gate's maximum age", Decided.StepUpRequired),
        ("a report made at an instant after now", Decided.StepUpRequired),
        ("a report that meets the gate, one of whose other instants is after now", Decided.StepUpRequired),
        ("a provider that fails to report", Decided.StepUpRequired),
        ("no provider", Decided.StepUpUnavailable),
    ];

    // An action bound to a consent-based purpose, on a sensitive type, which asks the
    // written consent of the record's data subject against the document the purpose
    // names. The caller holds the grant in every case, so what decides is the consent,
    // and the lists admit the record where the check does and nowhere else. A subject
    // holds a record a grant, so the cases include a live record beside an ended one and
    // a subject whose every record is ended, where the latest says which refusal it is
    // (AUTHZ-TEST-001 AC1, AUTHZ-GATE-002 AC4, PRIV-SENS-002 AC1, PRIV-CONS-007 AC5).
    private static readonly (string Scenario, Decided Decided)[] Consents =
    [
        ("a written consent against the document the purpose names", Decided.Allowed),
        ("no consent", Decided.ConsentRequired),
        ("a consent that was withdrawn", Decided.ConsentRequired),
        ("a consent that was superseded", Decided.ConsentSuperseded),
        ("a consent given again after one was withdrawn", Decided.Allowed),
        ("a live consent standing beside one that was superseded", Decided.Allowed),
        ("every consent to the purpose withdrawn or superseded", Decided.ConsentRequired),
        ("a written consent against another document", Decided.ConsentSuperseded),
        ("an ordinary consent where the purpose asks a written one", Decided.ConsentWrittenRequired),
        ("a consent to another purpose", Decided.ConsentRequired),
        ("a consent of the caller, who is not the record's data subject", Decided.ConsentRequired),
        ("a record that names no data subject", Decided.ConsentRequired),
    ];

    /// <summary>
    /// The table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> Cases => Read(Table);

    /// <summary>
    /// The step-up table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> StepUpCases => Read(StepUps);

    /// <summary>
    /// The operations' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> OperationCases => Read(Operations);

    /// <summary>
    /// The consents' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> ConsentCases => Read(Consents);

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
    /// AUTHZ-TEST-001 AC1, AC2, AUTHZ-GATE-005, LIB-HOST-004 AC3, AC4, AUTH-STEP-003 AC1:
    /// every case of the step-up table decides the way the table says through the single
    /// check, and both renderings of the filter answer the same: the record listed where
    /// the report meets the gate, and the filter refused with the code the check answers
    /// where it does not.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(StepUpCases))]
    public async Task AUTHZ_TEST_001_AC2_EveryStepUpCaseDecidesTheSameWayThroughBothPathsAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteBoundAsync();

        await using ServiceProvider? reporting = Reporting(scenario);

        Assert.Equal(decided, await ChecksAsync(written, reporting));
        Assert.Equal(decided, await ExpressionAdmitsAsync(written, reporting));
        Assert.Equal(decided, await FragmentAdmitsAsync(written, reporting));
    }

    /// <summary>
    /// AUTHZ-TEST-001 AC1, AUTHZ-GATE-002 AC4, PRIV-SENS-002 AC5: every case of the
    /// consents' table decides the way the table says through the single check, and both
    /// renderings of the filter list the record where the check admits it and nowhere
    /// else.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(ConsentCases))]
    public async Task AUTHZ_GATE_002_AC4_EveryConsentCaseDecidesTheSameWayThroughBothPathsAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteConsentedAsync(scenario);

        Decided listed = decided is Decided.Allowed ? Decided.Allowed : Decided.Denied;

        Assert.Equal(decided, await ChecksAsync(written));
        Assert.Equal(listed, await ExpressionAdmitsAsync(written));
        Assert.Equal(listed, await FragmentAdmitsAsync(written));
    }

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
        HostContext reading) =>
        Rendered(await FilteredAsync(written, reading, deployment: null));

    // The expression the gate renders for the case, or the refusal it answers before
    // rendering one (AUTHZ-GATE-005).
    private static async Task<Result<Expression<Func<HostDocument, bool>>>> FilteredAsync(
        Case written,
        HostContext reading,
        IServiceProvider? deployment)
    {
        await using AsyncServiceScope scope = (deployment ?? written.Host.Services).CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .FilterAsync(
                AccessContext.Of(written.Account),
                written.Asked,
                written.Record.Type,
                written.Deployment.Organization,
                Sources(reading),
                TestContext.Current.CancellationToken);
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

    // CONV-ERR-001, AUTHZ-PRIN-003: a type or a permission the model does not declare is
    // raised by the gate before anything is read, which is what the case decides; the
    // test's own failures are never read as one.
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
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
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
                    written.Asked,
                    written.Record,
                    Sources(reading),
                    TestContext.Current.CancellationToken);

            return outcome.Match(() => Decided.Allowed, Refused);
        });

    private async Task<Decided> ExpressionAdmitsAsync(Case written, IServiceProvider? deployment = null) =>
        await RaisedOrAsync(async () =>
        {
            await using HostContext reading = host.Context();

            return await (await FilteredAsync(written, reading, deployment)).Match(
                async expression => await reading.Documents
                    .Where(expression)
                    .AnyAsync(
                        document => document.Id == written.Record.Id.ToString(),
                        TestContext.Current.CancellationToken)
                    ? Decided.Allowed
                    : Decided.Denied,
                refused => Task.FromResult(Refused(refused)));
        });

    private async Task<Decided> FragmentAdmitsAsync(Case written, IServiceProvider? deployment = null) =>
        await RaisedOrAsync(async () =>
        {
            Result<SqlFilter> rendered;

            await using (AsyncServiceScope scope = (deployment ?? host.Services).CreateAsyncScope())
            {
                rendered = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                    .FragmentAsync(
                        AccessContext.Of(written.Account),
                        written.Asked,
                        written.Record.Type,
                        written.Deployment.Organization,
                        "identity_authz_row",
                        "id",
                        TestContext.Current.CancellationToken);
            }

            // AUTHZ-GATE-005: a fragment the gate refuses before rendering decides the
            // case by the code it answers.
            if (rendered.Match<Error?>(_ => null, refused => refused) is Error refusal)
            {
                return Refused(refusal);
            }

            SqlFilter fragment = Rendered(rendered);

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

        // CONV-ERR-001, AUTHZ-PRIN-003: a permission no rule governs is asked of a record
        // the whole organization's grant would otherwise admit.
        Permission asked = scenario == "a permission the model does not declare"
            ? Permission.Parse("document:share")
            : HostPermissions.Read;

        return new Case(host, deployment, account, record, sibling, inner, outer, asked);
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

    // Each operation case in a deployment of its own, its caller holding the reading and
    // management of grants and the management of groups across the organization, so
    // that what refuses a row no row names is the row's absence and never the caller's
    // want of a permission.
    private async Task<Decided> OperationAsync(string scenario)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend],
            cancellationToken);
        RoleName managing = await deployment.RoleAsync(
            [Permissions.GrantManage, Permissions.GrantRead, Permissions.GroupManage],
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

            case "a lookup of a record no registration names, by a caller reading grants":
                return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                        .WhoCanAccessAsync(context, Reference(Document), cancellationToken))
                    .Match(_ => Decided.Allowed, Refused);

            case "a change to the account's own settings":
            case "a change to the account's own settings, by a restricted caller":
                return await scope.ServiceProvider.GetRequiredService<ISettingsRestriction>()
                    .RefusedAsync(AccessContext.Of(caller), cancellationToken) is Error refused
                    ? Refused(refused)
                    : Decided.Allowed;

            case "a page's record whose subject gave the consent its purpose asks":
            case "a page's record whose subject gave no consent to its purpose":
                return await PagedAsync(
                    deployment,
                    role,
                    caller,
                    consented: scenario == "a page's record whose subject gave the consent its purpose asks");

            case "a page's record under a bound action, on a session that proved its gate":
                return await BoundPagedAsync(deployment, caller, TimeSpan.FromMinutes(1), downgraded: false);

            case "a page's record under a bound action, on a session whose proof has aged":
                return await BoundPagedAsync(deployment, caller, TimeSpan.FromDays(1), downgraded: false);

            case "a page's record under a bound action, on a session downgraded since it proved its gate":
                return await BoundPagedAsync(deployment, caller, TimeSpan.FromMinutes(1), downgraded: true);

            case "a grant in the administrative organization, to a member of it":
            case "a grant in the administrative organization, to an account holding no membership of it":
                return await AdministeredAsync(
                    deployment,
                    managing,
                    caller,
                    member: scenario == "a grant in the administrative organization, to a member of it");

            case "a check by background work, which holds no grant":
                return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                        .RequireAsync(
                            AccessContext.Of(SystemPrincipal.ForOrganization(
                                "import",
                                "the nightly import",
                                deployment.Organization)),
                            Permissions.GrantRead,
                            deployment.Organization,
                            cancellationToken))
                    .Match(() => Decided.Allowed, Refused);

            case "a check refused inside work the caller rolls back":
                return await RolledBackAsync(caller, deployment.Organization);

            case "a modifying check asked again inside the caller's unit of work":
                return await AskedAgainAsync(deployment, caller, restricted: false, settings: false);

            case "a modifying check asked again inside the caller's unit of work, by a caller restricted since the gate step":
                return await AskedAgainAsync(deployment, caller, restricted: true, settings: false);

            case "a settings change asked again inside its unit of work, by a caller restricted since the gate step":
                return await AskedAgainAsync(deployment, caller, restricted: true, settings: true);

            case "a group created, by a caller managing groups":
            case "a group created, by a caller restricted since the gate step":
                return await GroupCreatedAsync(
                    deployment,
                    caller,
                    restricted: scenario.EndsWith("since the gate step", StringComparison.Ordinal));

            case "a member a group already holds added again, by a caller managing groups":
                return await MemberChangedAsync(deployment, caller, held: true);

            case "a member a group does not hold taken out, by a caller managing groups":
                return await MemberChangedAsync(deployment, caller, held: false);

            case "a lookup of a record a fact in the host's data reaches, by a caller reading grants":
                return await ViewedAsync(deployment, caller);

            case "a fact in the host's data no grant was materialised for, after the drift check":
            case "a materialised grant the host's data no longer supports, after the drift check":
                return await DriftCheckedAsync(
                    deployment,
                    supported: scenario.StartsWith("a fact", StringComparison.Ordinal));

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "No such case.");
        }
    }

    // AUTHZ-DERIVE-007, LIB-HOST-001: the view of who can access a record a fact in the
    // host's data reaches, read through the relationship source the host declared. The
    // case is allowed where the view answers and names the fact's holder as derived.
    private async Task<Decided> ViewedAsync(Deployment deployment, SubjectId caller)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);
        SubjectId reviewing = await deployment.AccountAsync(cancellationToken);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.NamedRoleAsync(RoleName.Parse("reviewer"), [HostPermissions.Read], cancellationToken);
        await deployment.ReviewAsync(workspace, reviewing, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .WhoCanAccessAsync(AccessContext.Of(caller), record, cancellationToken))
            .Match(
                access => access.Grants.Any(grant =>
                    grant.Kind is GrantKind.Derived && grant.SubjectId == reviewing.Value)
                    ? Decided.Allowed
                    : throw new InvalidOperationException("The view left the derived grant out."),
                Refused);
    }

    // AUTHZ-DERIVE-005 AC4: a record under a workspace someone reviews, in the deployment
    // with the derivation materialised, after the drift check has run over the declared
    // source; where the case says the rows no longer support the grant, the fact is
    // taken away after a first check wrote it and the check runs again.
    private async Task<Decided> DriftCheckedAsync(Deployment deployment, bool supported)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);
        SubjectId reviewing = await deployment.AccountAsync(cancellationToken);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.NamedRoleAsync(RoleName.Parse("reviewer"), [HostPermissions.Read], cancellationToken);
        await deployment.ReviewAsync(workspace, reviewing, cancellationToken);

        await using ServiceProvider materialised = Materialised();

        await DriftCheckedAsync(materialised);

        if (!supported)
        {
            await deployment.UnreviewAsync(workspace, reviewing, cancellationToken);
            await DriftCheckedAsync(materialised);
        }

        await using AsyncServiceScope scope = materialised.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(AccessContext.Of(reviewing), HostPermissions.Read, record, cancellationToken))
            .Match(() => Decided.Allowed, Refused);
    }

    // The drift check as the worker runs it: the job, under its own principal.
    private static async Task DriftCheckedAsync(IServiceProvider deployment)
    {
        BackgroundJob check = BackgroundJobs.All.Single(job => job.Name == DerivationDriftCheck.Job);

        await using AsyncServiceScope scope = deployment.CreateAsyncScope();

        (await check.RunAsync(
                scope.ServiceProvider,
                AccessContext.Of(check.Principal),
                TestContext.Current.CancellationToken))
            .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
    }

    // AUTHZ-GATE-006 AC3: a modifying action that passed the gate step, asked again inside
    // the unit of work it writes in, where the account's row is held. A restriction
    // committed between the two refuses the second, and the case is decided by it.
    private async Task<Decided> AskedAgainAsync(
        Deployment deployment,
        SubjectId caller,
        bool restricted,
        bool settings)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope working = host.Services.CreateAsyncScope();

        IUnitOfWork work = working.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = AccessContext.Of(caller);

        async Task<Decided> AskAsync() =>
            settings
                ? await working.ServiceProvider.GetRequiredService<ISettingsRestriction>()
                    .RefusedAsync(context, cancellationToken) is Error refused
                    ? Refused(refused)
                    : Decided.Allowed
                : (await working.ServiceProvider.GetRequiredService<IAccessGate>()
                        .RequireAsync(context, Permissions.GrantManage, deployment.Organization, cancellationToken))
                    .Match(() => Decided.Allowed, Refused);

        Assert.Equal(Decided.Allowed, await AskAsync());

        if (restricted)
        {
            await deployment.RestrictAsync(caller, cancellationToken);
        }

        _ = await work.BeginAsync(cancellationToken);

        Decided decided = await AskAsync();

        await work.RollbackAsync();

        return decided;
    }

    // AUTHZ-GATE-006 AC3, CONV-DESIGN-002: a change through the library's own operation,
    // which asks the gate at its gate step and again inside its unit of work. Where the
    // case restricts the caller between the two, the operation refuses, leaves no
    // transaction open and writes no group.
    private async Task<Decided> GroupCreatedAsync(Deployment deployment, SubjectId caller, bool restricted)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using ServiceProvider interleaved = Interleaved(
            async cancelled =>
            {
                if (restricted)
                {
                    await deployment.RestrictAsync(caller, cancelled);
                }
            });
        await using AsyncServiceScope scope = interleaved.CreateAsyncScope();

        Decided decided = (await scope.ServiceProvider.GetRequiredService<IGroups>()
                .CreateAsync(
                    AccessContext.Of(caller),
                    deployment.Organization,
                    "Reviewers",
                    "A group for the reviewers.",
                    cancellationToken))
            .Match(_ => Decided.Allowed, Refused);

        await using NpgsqlConnection connection = await host.OpenAsync();

        int written = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM identity.groups WHERE organization = @organization;",
            new { organization = deployment.Organization.Value },
            cancellationToken: cancellationToken));

        Assert.False(Assert.IsType<UnitOfWorkInterleaved>(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>()).Open);
        Assert.Equal(decided is Decided.Allowed ? 1 : 0, written);

        return decided;
    }

    // A session of the caller that proved two factors a minute ago, which meets the gate
    // a change of members asks.
    private static async Task<SessionId> SteppedUpAsync(IServiceProvider services, SubjectId caller)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            caller,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Deployment.Noon - TimeSpan.FromMinutes(1),
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(caller, cancellationToken) is null)
        {
            await keys.CreateAsync(caller, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        return session.Id;
    }

    // AUTHZ-GROUP-001, CONV-DESIGN-003 AC10: a change of members that changes nothing is
    // decided as any other, the gate asked at its step and again inside the unit of work
    // and the step-up met, and is answered as done; it writes no row and leaves no
    // transaction open.
    private async Task<Decided> MemberChangedAsync(Deployment deployment, SubjectId caller, bool held)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        var member = GrantSubject.Of(account);
        var context = AccessContext.Of(caller);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;
        SessionId session = await SteppedUpAsync(services, caller);
        IGroups groups = services.GetRequiredService<IGroups>();

        GroupId group = (await groups.CreateAsync(
                context,
                deployment.Organization,
                "Reviewers",
                "A group for the reviewers.",
                cancellationToken))
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        if (held)
        {
            (await groups.AddMemberAsync(context, session, group, member, "Joined the reviewers.", cancellationToken))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }

        Decided decided = (held
                ? await groups.AddMemberAsync(context, session, group, member, "Joined the reviewers.", cancellationToken)
                : await groups.RemoveMemberAsync(context, session, group, member, "Left the reviewers.", cancellationToken))
            .Match(() => Decided.Allowed, Refused);

        await using NpgsqlConnection connection = await host.OpenAsync();

        int members = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM identity.group_members WHERE group_id = @group;",
            new { group = group.Value },
            cancellationToken: cancellationToken));

        Assert.Equal(held ? 1 : 0, members);

        return decided;
    }

    // AUTHZ-CONCEAL-004 AC4: a check the caller makes inside a unit of work it then rolls
    // back, decided as any other and resolving afterwards by the identifier it carried.
    private async Task<Decided> RolledBackAsync(SubjectId caller, OrganizationId organization)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Result outcome;

        await using (AsyncServiceScope working = host.Services.CreateAsyncScope())
        {
            IUnitOfWork work = working.ServiceProvider.GetRequiredService<IUnitOfWork>();

            _ = await work.BeginAsync(cancellationToken);
            outcome = await working.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(AccessContext.Of(caller), HostPermissions.Publish, organization, cancellationToken);
            await work.RollbackAsync();
        }

        if (outcome.Match<Error?>(() => null, error => error) is not Error refusal)
        {
            return Decided.Allowed;
        }

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        AccessExplanation resolved = (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .ResolveOwnAsync(
                    AccessContext.Of(caller),
                    new AuditRecordId(refusal.Details["correlation"].GetGuid()),
                    cancellationToken))
            .Match(explained => explained, error => throw new InvalidOperationException(error.Code.ToString()));

        return resolved.Outcome is AccessOutcome.Denied ? Refused(refusal) : Decided.Allowed;
    }

    // IDN-LIFE-009a, D-166: the caller's grant in the administrative organization,
    // asked of that organization as an administrative operation asks it.
    private async Task<Decided> AdministeredAsync(
        Deployment deployment,
        RoleName managing,
        SubjectId caller,
        bool member)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);

        if (member)
        {
            await deployment.MemberAsync(caller, administrative, cancellationToken);
        }

        await deployment.GrantAsync(
            GrantSubject.Of(caller), managing, null, false, null, administrative, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(AccessContext.Of(caller), Permissions.GrantRead, administrative, cancellationToken))
            .Match(() => Decided.Allowed, Refused);
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

    // AUTHZ-GATE-005, AUTH-SESS-009: a page under an action bound to a step-up gate,
    // asked on the caller's own session. The page names what the action still requires
    // and the single check refuses it for step-up, or both admit it; a session
    // downgraded since its proof is asked to authenticate again, and one whose proof has
    // aged to step up.
    private async Task<Decided> BoundPagedAsync(
        Deployment deployment,
        SubjectId caller,
        TimeSpan ago,
        bool downgraded)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);
        RoleName publishing = await deployment.RoleAsync(
            [HostPermissions.Read, HostPermissions.Publish],
            cancellationToken);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(caller), publishing, workspace, false, null, null, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        IServiceProvider services = scope.ServiceProvider;

        var session = Session.Begin(
            SessionId.New(TimeProvider.System),
            caller,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            Deployment.Noon - ago,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

        if (downgraded)
        {
            session.Downgrade(Deployment.Noon - (ago / 2));
        }

        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(caller, cancellationToken) is null)
        {
            await keys.CreateAsync(caller, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);

        services.GetRequiredService<RequestSession>().Resolved(session);

        IAccessGate gate = services.GetRequiredService<IAccessGate>();

        Capability paged = Rendered(await gate.CapabilitiesAsync(
                AccessContext.Of(caller),
                Document,
                [record.Id],
                [HostPermissions.Publish],
                Sources(reading),
                cancellationToken))
            .Single();

        Decided checkedAlone = (await gate.RequireAsync(
                AccessContext.Of(caller),
                HostPermissions.Publish,
                record,
                Sources(reading),
                cancellationToken))
            .Match(() => Decided.Allowed, Refused);

        Assert.Contains(HostPermissions.Publish, paged.Can);

        Decided asked = paged.Requires.TryGetValue(
            HostPermissions.Publish,
            out IReadOnlySet<CapabilityResidual>? outstanding)
            ? outstanding.SetEquals([CapabilityResidual.Reauthenticate])
                ? Decided.ReauthenticationRequired
                : outstanding.SetEquals([CapabilityResidual.StepUp])
                    ? Decided.StepUpRequired
                    : throw new InvalidOperationException("The page asks what the table has no row for.")
            : Decided.Allowed;

        Assert.Equal(
            asked is Decided.Allowed ? Decided.Allowed : Decided.StepUpRequired,
            checkedAlone);

        return asked;
    }

    // A record the account's grant confers the consent-bound action on, whose data
    // subject holds what the case names.
    private async Task<Case> WriteConsentedAsync(string scenario)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend],
            cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        SubjectId subject = await deployment.AccountAsync(cancellationToken);
        ResourceReference outer = Reference(Workspace);
        ResourceReference inner = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference sibling = Reference(Document);

        await deployment.RegisterAsync(outer, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(inner, outer, cancellationToken);
        await deployment.RegisterAsync(sibling, inner, cancellationToken);
        await deployment.RegisterAsync(
            record,
            inner,
            cancellationToken,
            scenario == "a record that names no data subject" ? null : subject);
        await deployment.GrantAsync(GrantSubject.Of(account), role, record, false, null, null, cancellationToken);

        switch (scenario)
        {
            case "a written consent against the document the purpose names":
                await ConsentedAsync(subject);
                break;
            case "a consent that was withdrawn":
                await ConsentedAsync(subject);
                await EndedAsync(subject, withdrawn: true);
                break;
            case "a consent that was superseded":
                await ConsentedAsync(subject);
                await EndedAsync(subject, withdrawn: false);
                break;
            case "a consent given again after one was withdrawn":
                await ConsentedAsync(subject);
                await EndedAsync(subject, withdrawn: true);
                await ConsentedAsync(subject);
                break;
            case "a live consent standing beside one that was superseded":
                await ConsentedAsync(subject);
                await EndedAsync(subject, withdrawn: false);
                await ConsentedAsync(subject, after: TimeSpan.FromHours(2));
                break;
            case "every consent to the purpose withdrawn or superseded":
                await ConsentedAsync(subject);
                await EndedAsync(subject, withdrawn: false);
                await ConsentedAsync(subject, after: TimeSpan.FromHours(2));
                await EndedAsync(subject, withdrawn: true, after: TimeSpan.FromHours(3));
                break;
            case "a written consent against another document":
                await ConsentedAsync(subject, document: "newsletter-terms");
                break;
            case "an ordinary consent where the purpose asks a written one":
                await ConsentedAsync(subject, kind: ConsentKind.Ordinary);
                break;
            case "a consent to another purpose":
                await ConsentedAsync(subject, purpose: "newsletters");
                break;
            case "a consent of the caller, who is not the record's data subject":
            case "a record that names no data subject":
                await ConsentedAsync(account);
                break;
            case "no consent":
                break;
            default:
                throw new InvalidOperationException("The consents' table holds a case nothing writes.");
        }

        return new Case(host, deployment, account, record, sibling, inner, outer, HostPermissions.Recommend);
    }

    // What the page decides on one record: the consent it still requires, the action
    // it admits, or neither.
    private static Decided Paged(Capability capability) =>
        capability.Requires.TryGetValue(HostPermissions.Recommend, out IReadOnlySet<CapabilityResidual>? outstanding)
            && outstanding.SetEquals([CapabilityResidual.Consent])
            ? Decided.ConsentRequired
            : capability.Can.Contains(HostPermissions.Recommend) ? Decided.Allowed : Decided.Denied;

    // PRIV-SENS-002 AC1: the written consent the consent-based purpose asks of a
    // sensitive type, recorded for its data subject. A case that gives a second consent
    // gives it later than the first, so which of the two is the latest is not left to
    // their identifiers.
    private async Task ConsentedAsync(
        SubjectId subject,
        string purpose = "recommendations",
        string document = "privacy-notice",
        ConsentKind kind = ConsentKind.Written,
        TimeSpan after = default)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);
        _ = await scope.ServiceProvider.GetRequiredService<IConsentStore>().AddAsync(
            subject,
            new ConsentRecord(
                purpose,
                document,
                "1",
                ConsentMechanism.Dashboard,
                kind,
                Deployment.Noon + after,
                WithdrawnAt: null,
                SupersededAt: null),
            cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    // PRIV-CONS-004, PRIV-CONS-007: the subject's live consent to the purpose, stamped
    // withdrawn or superseded, an hour after the first consent where the case names no
    // other instant.
    private async Task EndedAsync(SubjectId subject, bool withdrawn, TimeSpan? after = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IConsentStore store = scope.ServiceProvider.GetRequiredService<IConsentStore>();
        DateTimeOffset at = Deployment.Noon + (after ?? TimeSpan.FromHours(1));

        await work.BeginAsync(cancellationToken);

        Assert.True(withdrawn
            ? await store.WithdrawConsentAsync(subject, "recommendations", at, cancellationToken)
            : await store.SupersedeAsync(subject, "recommendations", at, cancellationToken));

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

            // The whole organization is granted, so a type or a permission the gate
            // answered rather than raised would be allowed.
            case "a record of a type the model does not declare":
            case "a permission the model does not declare":
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

    // What a provider reports in each case of the step-up table, and nothing where the
    // case has it fail.
    private static AttainedAssurance? Reported(string scenario)
    {
        DateTimeOffset recent = Deployment.Noon - TimeSpan.FromMinutes(1);
        DateTimeOffset aged = Deployment.Noon - TimeSpan.FromMinutes(6);
        DateTimeOffset ahead = Deployment.Noon + TimeSpan.FromMinutes(1);

        var met = new AttainedAssurance(
            Aal1At: recent,
            Aal2At: recent,
            Aal3At: null,
            PhishingResistantAt: recent,
            AssuranceLevel.Aal2);

        return scenario switch
        {
            "a report that meets the gate" => met,
            "a report below the level the gate asks" => met with { Aal2At = null },
            "a report that last reached the gate's level before its maximum age, and a lower one within it" => met with { Aal2At = aged },
            "a report that last reached a level above the gate's within its maximum age" => met with { Aal2At = aged, Aal3At = recent },
            "a report that was not phishing-resistant, at a gate asking it" => met with { PhishingResistantAt = null },
            "a report that last reached phishing resistance before the gate's maximum age" => met with { PhishingResistantAt = aged },
            "a report older than the gate's maximum age" => met with { Aal1At = aged, Aal2At = aged, PhishingResistantAt = aged },
            "a report made at an instant after now" => met with { Aal1At = ahead, Aal2At = ahead, PhishingResistantAt = ahead },
            "a report that meets the gate, one of whose other instants is after now" => met with { Aal3At = ahead },
            "a provider that fails to report" => null,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "The table has no such case."),
        };
    }

    // The same deployment with a host's assurance provider registered, which is what a
    // deployment consuming authorization without the library's sign-in supplies
    // (LIB-HOST-004); the last case registers none, as the fixture does.
    private ServiceProvider? Reporting(string scenario)
    {
        if (scenario == "no provider")
        {
            return null;
        }

        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        services.AddSingleton<IAssuranceProvider>(new AssuranceProviderInMemory(Reported(scenario)));
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // A record the account's grant confers the bound action on, in an organization the
    // account is a member of and whose policy states the strict gate.
    private async Task<Case> WriteBoundAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Publish],
            cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        ResourceReference outer = Reference(Workspace);
        ResourceReference inner = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference sibling = Reference(Document);

        await deployment.RegisterAsync(outer, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(inner, outer, cancellationToken);
        await deployment.RegisterAsync(sibling, inner, cancellationToken);
        await deployment.RegisterAsync(record, inner, cancellationToken);
        await deployment.MemberAsync(account, deployment.Organization, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(account), role, record, false, null, null, cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO identity.settings (key, value) VALUES (@key, @value);",
            new
            {
                key = Settings.OrganizationPolicy.For(deployment.Organization.ToString()).ToString(),
                value = Settings.OrganizationPolicy.Write(PolicyOverride.None with
                {
                    Gates = new Dictionary<StepUpAction, Gate> { [StepUpAction.IdentifierAdd] = Strict },
                }),
            },
            cancellationToken: cancellationToken));

        return new Case(host, deployment, account, record, sibling, inner, outer, HostPermissions.Publish);
    }

    // The same deployment with something committed on another connection in the moment
    // before an operation's unit of work begins, which is after its gate step
    // (AUTHZ-GATE-006 AC3).
    private ServiceProvider Interleaved(Func<CancellationToken, Task> meanwhile)
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public);
        services.AddScoped<IUnitOfWork>(provider =>
            new UnitOfWorkInterleaved(new UnitOfWork(provider.GetRequiredService<StoreContext>()), meanwhile));

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // The same deployment with the one derivation precomputed into grant rows, which
    // is what AUTHZ-TEST-001 AC3 asks the table of a second time.
    private ServiceProvider Materialised()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        HostFixture.Sourced(services, host.ConnectionString);
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(materialised: true), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
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
        ResourceReference Outer,
        Permission Asked);
}
