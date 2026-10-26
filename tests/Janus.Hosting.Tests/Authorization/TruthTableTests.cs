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
using Janus.Authentication.Tests;
using Janus.Authorization.Gate;
using Janus.Authorization.Model;
using Janus.Authorization.Roles;
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
        ("a grant on a record whose type declares its encrypted field under a name of its own", Decided.Allowed),
        ("a grant on a record whose type no other type's declaration shares a name with", Decided.Allowed),
        ("a grant on a record whose type declares no two encrypted fields under one name", Decided.Allowed),
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
        ("a grant on a record whose registration was removed, the record registered again", Decided.Denied),
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
        ("a host permission asked by background work under a principal naming no declared job", Decided.Denied),
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
        (
            "a member added to a group, on a session that has not proved its gate, by a caller managing groups",
            Decided.StepUpRequired),
        (
            "a member taken out of a group, on a session that has not proved its gate, by a caller managing groups",
            Decided.StepUpRequired),
        ("a role defined, on a session that has not proved its gate, by a caller managing roles", Decided.StepUpRequired),
        ("a grant written and then revoked, by a caller managing grants", Decided.Allowed),
        (
            "a grant written and then revoked in another organization, by a system administrator of the administrative organization",
            Decided.Allowed),
        (
            "a grant written in another organization, by a system administrator restricted since the gate step",
            Decided.Restricted),
        (
            "a grant on the whole of an organization the deployment does not hold, by a system administrator managing grants",
            Decided.Denied),
        (
            "a grant on the whole of an organization the deployment does not hold, by a restricted system administrator",
            Decided.Restricted),
        ("a grant on a record whose registration was removed since the gate step, by a caller managing grants", Decided.Denied),
        ("a lookup of a record a fact in the host's data reaches, by a caller reading grants", Decided.Allowed),
        ("a fact in the host's data no grant was materialised for, after the drift check", Decided.Allowed),
        ("a materialised grant the host's data no longer supports, after the drift check", Decided.Denied),
        ("a fact in the host's data a declared job's refresh materialised, outside a request", Decided.Allowed),
        ("a fact in the host's data a declared job's refresh was asked for, while a request is served", Decided.Denied),
        ("a fact in the host's data a principal naming no declared job asked a refresh for", Decided.Denied),
        ("a fact in the host's data a declared job acting for another organization asked a refresh for", Decided.Denied),
        ("a fact in the host's data a reconciling principal's refresh materialised, outside a request", Decided.Allowed),
        ("a fact in the host's data a reconciling principal asked a refresh for, while a request is served", Decided.Denied),
        ("a fact in the host's data a principal that may not reconcile asked a refresh for", Decided.Denied),
        ("a page asked for a declared permission its type does not list as a capability", Decided.Raised),
        ("a type declaring as capabilities two permissions of one action", Decided.Raised),
        ("a step-up gate the host bound to a library-owned permission", Decided.Raised),
        ("a purpose the host bound to a library-owned permission", Decided.Raised),
        ("a role defined with the context of the start principal role-declaration", Decided.Denied),
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

    // An action bound to a step-up gate, judged from the library's own session where it
    // carries the request and is the acting person's own: what the session last reached,
    // and when, against the gate (AUTHZ-TEST-001 AC1, AUTH-STEP-002 step 1,
    // AUTH-SESS-001, AUTH-SESS-009). The record, the grant and the gate are the step-up
    // table's, so what decides is the session alone, and a list asked under the action
    // is refused with the code the check answers (AUTHZ-TEST-001 AC2, AUTHZ-GATE-005).
    // These rows are the library's own and no scenario of chapter 10 section 5.30, since
    // the conformance suite judges a step-up from a host's report alone.
    private static readonly (string Scenario, Decided Decided)[] Sessions =
    [
        ("a session that meets the gate", Decided.Allowed),
        ("a session below the level the gate asks", Decided.StepUpRequired),
        ("a session that was not phishing-resistant, at a gate asking it", Decided.StepUpRequired),
        ("a session whose proof is older than the gate's maximum age", Decided.StepUpRequired),
        (
            "a session that reached a lower level since, beside the gate's level past its maximum age",
            Decided.StepUpRequired),
        ("a session whose proof was reached only before its last downgrade", Decided.StepUpRequired),
        (
            "a session that reached delegated alone, the level whose own gate asks no maximum age",
            Decided.StepUpRequired),
        ("a session derived from a record that meets the gate", Decided.Allowed),
        (
            "a session derived within the gate's maximum age from a record whose proof is older than it",
            Decided.StepUpRequired),
        (
            "a session derived after its record's last downgrade, the record's proof reached only before it",
            Decided.StepUpRequired),
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

    // A host's declared job asking a host permission on a record. While a request is
    // served, through either profile or neither and in a scope opened within the
    // request, the check and both renderings are refused with the code the check
    // answers, a job being background work (D-206). Outside a request its declaration
    // reaches every record of its organization, and no restriction is judged for it, a
    // restriction being the acting account's; it is refused for any other permission,
    // in another organization, and in its own once its deletion is requested or it is
    // erased. A permission bound to a consent-based purpose asks the record's data
    // subject's consent of a job as of a person. The lists admit the record where the
    // check does, and a list the declaration does not reach is refused with the code the
    // check answers (AUTHZ-TEST-001 AC1, AC2, IDN-PRIN-001 AC6, PRIV-RIGHT-004 AC3, D-207,
    // D-208).
    private static readonly (string Scenario, Decided Decided)[] Jobs =
    [
        ("a declared job asking while a request is served through the browser profile", Decided.Denied),
        ("a declared job asking while a request is served through the machine profile", Decided.Denied),
        ("a declared job asking while a request is served outside both profiles", Decided.Denied),
        ("a declared job asking in a scope opened within a request", Decided.Denied),
        ("a declared job asking a permission its declaration names, outside a request", Decided.Allowed),
        (
            "a declared job asking a modifying permission its declaration names, on a record whose data subject is restricted",
            Decided.Allowed),
        ("a declared job asking a permission its declaration does not name", Decided.Denied),
        (
            "a declared job asking a consent-bound permission its declaration names, on a record whose data subject consented",
            Decided.Allowed),
        (
            "a declared job asking a consent-bound permission its declaration names, on a record whose data subject has not consented",
            Decided.ConsentRequired),
        ("a declared job asking on a record of another organization", Decided.Denied),
        ("a declared job asking on a record the library holds no row for", Decided.Denied),
        ("a declared job asking in its organization, whose deletion is requested", Decided.Denied),
        ("a declared job asking in its organization, once it is erased", Decided.Denied),
    ];

    // A host's declared job asking a host permission of its organization itself, which
    // names no record and so runs through the single check alone: its declaration
    // reaches the organization as an organization-wide grant reaches a person, and the
    // refusals are those of its records; a consent-based purpose, which no data subject's
    // consent can admit there, is refused as it is for a person (AUTHZ-TEST-001 AC1, AC2,
    // IDN-PRIN-001 AC6, AUTHZ-GATE-005, D-207, D-208).
    private static readonly (string Scenario, Decided Decided)[] JobOrganizations =
    [
        ("a declared job asking a permission its declaration names, of its organization", Decided.Allowed),
        ("a declared job asking a permission its declaration does not name, of its organization", Decided.Denied),
        ("a declared job asking a consent-bound permission its declaration names, of its organization", Decided.ConsentRequired),
        ("a declared job asking of another organization", Decided.Denied),
        ("a declared job asking of its organization, whose deletion is requested", Decided.Denied),
        ("a declared job asking of its organization, once it is erased", Decided.Denied),
        ("a declared job asking of its organization while a request is served", Decided.Denied),
    ];

    // A grant written in an organization, which names no record of a list and so runs
    // through the grant admission alone: in an organization other than the
    // administrative organization, grant:manage held there or system:administer held in
    // the administrative organization admits it, either recording no refusal, and where
    // neither does one refusal is recorded, naming grant:manage in the grant's
    // organization; a restriction is judged first and records nothing; in the
    // administrative organization grant:manage alone admits (AUTHZ-TEST-001 AC1, AC2,
    // AUTHZ-SCOPE-001 AC4, AUTHZ-CONCEAL-004 AC5, D-207, D-208).
    private static readonly (string Scenario, Decided Decided)[] GrantAdmissions =
    [
        ("a grant in another organization, by a caller managing grants there", Decided.Allowed),
        ("a grant in another organization, by a system administrator of the administrative organization", Decided.Allowed),
        ("a grant in another organization, by a caller managing grants in the administrative organization alone", Decided.Denied),
        (
            "a grant in another organization, by a system administrator holding no membership of the administrative organization",
            Decided.Denied),
        ("a grant in another organization, by a system administrator denied grant management there", Decided.Allowed),
        ("a grant in another organization, by a restricted system administrator", Decided.Restricted),
        ("a grant in another organization, by a restricted caller managing grants there", Decided.Restricted),
        ("a grant in another organization, by background work", Decided.Denied),
        ("a grant in the administrative organization, by a system administrator holding nothing else there", Decided.Denied),
    ];

    // A restricted account asking a host permission on a record of its organization, its
    // grant on the record conferring the reading and the modifying permission save where
    // the case names none. A restriction is the account's and not a row's, so the check,
    // the expression and the fragment refuse a modifying permission authz.restricted
    // before any rule is handed out, whatever the grants confer, and hand a reading one
    // its rule; none records a refusal (AUTHZ-TEST-001 AC1, AC2, AUTHZ-GATE-006 AC5,
    // AUTHZ-CONCEAL-004, D-209).
    private static readonly (string Scenario, Decided Decided)[] Restrictions =
    [
        ("a restricted account asking a modifying permission its grant confers", Decided.Restricted),
        ("a restricted account asking a modifying permission no grant confers", Decided.Restricted),
        ("a restricted account asking a reading permission its grant confers", Decided.Allowed),
    ];

    // An action bound to a step-up gate asked twice in one request, first through the
    // single check and then again, with something between the two asks that would change
    // the gate's judgement were it judged anew: the gate is judged once a request, from
    // the host's report or from the library's session, and the second ask answers as
    // the first did (AUTH-STEP-002 AC10, AUTHZ-GATE-006, D-210). The record, the grant
    // and the gate are the step-up table's. These rows are the library's own and no
    // scenario of chapter 10 section 5.30, since the suite asks each gate once.
    private static readonly (string Scenario, Decided Decided)[] Requests =
    [
        (
            "a report that met the gate at the first ask in a request, older than its maximum age at a second ask in it",
            Decided.Allowed),
        (
            "a report that did not meet the gate at the first ask in a request, meeting it at a second ask in it",
            Decided.StepUpRequired),
        (
            "a session that met the gate at the first ask in a request, its proof older than its maximum age at a second ask in it",
            Decided.Allowed),
        (
            "a session that did not meet the gate at the first ask in a request, lifted to it by another request's step-up before a second ask in it",
            Decided.StepUpRequired),
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
    /// The sessions' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> SessionCases => Read(Sessions);

    /// <summary>
    /// The operations' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> OperationCases => Read(Operations);

    /// <summary>
    /// The consents' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> ConsentCases => Read(Consents);

    /// <summary>
    /// The jobs' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> JobCases => Read(Jobs);

    /// <summary>
    /// The jobs' organization table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> JobOrganizationCases => Read(JobOrganizations);

    /// <summary>
    /// The grant admissions' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> GrantAdmissionCases => Read(GrantAdmissions);

    /// <summary>
    /// The restrictions' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> RestrictedCases => Read(Restrictions);

    /// <summary>
    /// The requests' table as the run reads it.
    /// </summary>
    public static TheoryData<string, Decided> RequestCases => Read(Requests);

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
    /// AUTHZ-TEST-001 AC1, AC2, AUTHZ-GATE-005, AUTH-STEP-002 AC3, AUTH-SESS-001 AC3,
    /// AUTH-SESS-009, AUTH-SESS-012 AC8: every case of the sessions' table decides the
    /// way the table says through the single check, and both renderings of the filter
    /// answer the same: the record listed where the session meets the gate, and the
    /// filter refused with the code the check answers where it does not.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(SessionCases))]
    public async Task AUTHZ_TEST_001_AC2_EverySessionCaseDecidesTheSameWayThroughBothPathsAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteBoundAsync();
        IReadOnlyList<Session> proving = Proving(written.Account, scenario);
        Session carried = proving[^1];

        foreach (Session kept in proving)
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

            await KeptAsync(scope.ServiceProvider, kept);
        }

        Assert.Equal(decided, await ChecksAsync(written, carried: carried));
        Assert.Equal(decided, await ExpressionAdmitsAsync(written, carried: carried));
        Assert.Equal(decided, await FragmentAdmitsAsync(written, carried: carried));
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
    /// AUTHZ-TEST-001 AC1, AC2, IDN-PRIN-001 AC6 (D-206, D-207), PRIV-RIGHT-004 AC3: every
    /// case of the jobs' table decides the way the table says through the single check,
    /// the expression and the fragment alike, each asked inside the request the case
    /// serves or in background work.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(JobCases))]
    public async Task AUTHZ_TEST_001_AC2_EveryJobCaseDecidesTheSameWayThroughBothPathsAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteJobAsync(scenario);

        if (!scenario.Contains("request is served", StringComparison.Ordinal)
            && !scenario.Contains("within a request", StringComparison.Ordinal))
        {
            // AUTHZ-GATE-002 AC4: a list leaves out a record whose consent is outstanding,
            // as it leaves out one its grants do not reach.
            Decided listed = decided is Decided.ConsentRequired ? Decided.Denied : decided;

            Assert.Equal((decided, listed, listed), await JobListedAsync(written, scenario));

            return;
        }

        (Decided Check, Decided Expression, Decided Fragment)? answered = null;

        await using ServedRequests served = await ServedRequests.StartAsync(
            host,
            async context =>
            {
                if (scenario == "a declared job asking in a scope opened within a request")
                {
                    await using AsyncServiceScope within = context.RequestServices
                        .GetRequiredService<IServiceScopeFactory>()
                        .CreateAsyncScope();

                    answered = await JobAskedAsync(within.ServiceProvider, written);
                }
                else
                {
                    answered = await JobAskedAsync(context.RequestServices, written);
                }
            },
            TestContext.Current.CancellationToken);

        _ = await served.SendAsync(scenario switch
        {
            "a declared job asking while a request is served through the machine profile" => ServedRequests.Machine,
            "a declared job asking while a request is served outside both profiles" => ServedRequests.Outside,
            _ => ServedRequests.Browser,
        });

        Assert.Equal((decided, decided, decided), answered);
    }

    /// <summary>
    /// AUTHZ-TEST-001 AC1, AC2, IDN-PRIN-001 AC6 (D-207): every case of the jobs'
    /// organization table decides the way the table says through the single check, the
    /// one path a case naming no record runs through.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(JobOrganizationCases))]
    public async Task AUTHZ_TEST_001_AC2_EveryJobOrganizationCaseDecidesTheWayTheTableSaysThroughTheCheckAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteJobAsync(scenario);
        AccessContext job = await JobOfAsync(written, scenario);
        Decided? answered = null;

        async Task<Decided> CheckedAsync(IServiceProvider asking) =>
            (await asking.GetRequiredService<IAccessGate>().RequireAsync(
                    job,
                    written.Asked,
                    written.Deployment.Organization,
                    TestContext.Current.CancellationToken))
                .Match(() => Decided.Allowed, Refused);

        if (scenario.EndsWith("while a request is served", StringComparison.Ordinal))
        {
            await using ServedRequests served = await ServedRequests.StartAsync(
                host,
                async context => answered = await CheckedAsync(context.RequestServices),
                TestContext.Current.CancellationToken);

            _ = await served.SendAsync(ServedRequests.Browser);
        }
        else
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

            answered = await CheckedAsync(scope.ServiceProvider);
        }

        Assert.Equal(decided, answered);
    }

    /// <summary>
    /// AUTHZ-TEST-001 AC1, AC2, AUTHZ-SCOPE-001 AC4, AUTHZ-CONCEAL-004 AC5 (D-207, D-208):
    /// every case of the grant admissions' table decides the way the table says through
    /// the grant admission, the one path a grant written in an organization runs
    /// through, and a refusal is recorded once, naming <c>grant:manage</c> in the grant's
    /// organization, where it is denied, and nothing is recorded otherwise.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(GrantAdmissionCases))]
    public async Task AUTHZ_TEST_001_AC2_EveryGrantAdmissionCaseDecidesTheWayTheTableSaysThroughTheAdmissionAsync(
        string scenario,
        Decided decided)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        (AccessContext context, OrganizationId organization) = await AdmittingAsync(scenario);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result admitted = await scope.ServiceProvider.GetRequiredService<IGrantAdmission>()
            .AdmitAsync(context, organization, cancellationToken);

        Assert.Equal(decided, admitted.Match(() => Decided.Allowed, Refused));
        Assert.Equal(decided is Decided.Denied ? 1 : 0, await GrantRefusalsAsync(context, organization));

        if (admitted.Match<Error?>(() => null, error => error) is Error refusal && refusal.Code == ErrorCodes.Denied)
        {
            DeniedAccess? recorded = await scope.ServiceProvider.GetRequiredService<IAccessAudit>().FindAsync(
                new AuditRecordId(refusal.Details["correlation"].GetGuid()),
                cancellationToken);

            Assert.Equal(Permissions.GrantManage, recorded?.Permission);
            Assert.Equal(organization, recorded?.Organization);
        }
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 AC4, IDN-ORG-003 (D-207, D-208): through the library's grant
    /// operations and the gate's own admission, an organization
    /// <see cref="IOrganizations.CreateAsync"/> created holds no grant, and a holder of
    /// <c>system:administer</c> in the administrative organization writes its first grant
    /// and revokes it, each asking the admission at its gate step and again inside its
    /// unit of work, with no refusal recorded; a holder of <c>grant:manage</c> in the
    /// administrative organization alone is refused there with one refusal recorded,
    /// naming <c>grant:manage</c> in the created organization, and nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AC4_ASystemAdministratorWritesAndRevokesTheFirstGrantOfACreatedOrganizationAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName role = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        SubjectId administrator = await AdministrationMemberAsync(
            deployment,
            administrative,
            [Permissions.SystemAdminister, Permissions.OrganizationManage]);
        SubjectId manager = await AdministrationMemberAsync(deployment, administrative, [Permissions.GrantManage]);
        SubjectId account = await deployment.AccountAsync(cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IGrants grants = scope.ServiceProvider.GetRequiredService<IGrants>();
        OrganizationId created = (await scope.ServiceProvider.GetRequiredService<IOrganizations>().CreateAsync(
                AccessContext.Of(administrator),
                "Branch " + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture),
                "Opening a branch.",
                cancellationToken))
            .Match(organization => organization, error => throw new InvalidOperationException(error.Code.ToString()));
        GrantRequest first = OrganizationWideGrant(account, role, created);

        Assert.Equal(0, await GrantsInAsync(created));

        SessionId administering = await SteppedUpAsync(scope.ServiceProvider, administrator);
        GrantId written = (await grants.GrantAsync(AccessContext.Of(administrator), administering, first, cancellationToken))
            .Match(grant => grant, error => throw new InvalidOperationException(error.Code.ToString()));
        Result revoked = await grants.RevokeAsync(
            AccessContext.Of(administrator),
            administering,
            written,
            "No longer needed.",
            cancellationToken);

        Assert.True(revoked.Match(() => true, _ => false));
        Assert.Equal(0, await GrantRefusalsAsync(AccessContext.Of(administrator), created));

        Result<GrantId> refused = await grants.GrantAsync(
            AccessContext.Of(manager),
            await SteppedUpAsync(scope.ServiceProvider, manager),
            first,
            cancellationToken);
        DeniedAccess? recorded = await RecordedRefusalAsync(scope.ServiceProvider, refused);

        Assert.Equal(1, await GrantRefusalsAsync(AccessContext.Of(manager), created));
        Assert.Equal(Permissions.GrantManage, recorded?.Permission);
        Assert.Equal(created, recorded?.Organization);
        Assert.Equal(1, await GrantsInAsync(created));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 AC4, AUTHZ-GATE-006 (D-208, D-209): an acting account under
    /// restriction is refused <c>authz.restricted</c>, with no refusal recorded and nothing
    /// written, though it holds <c>system:administer</c> in the administrative
    /// organization and <c>grant:manage</c> in the grant's: a grant there, a revocation
    /// there, and a grant on the whole of an organization the deployment does not hold.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AC4_ARestrictedCallerIsRefusedWithNoRecordWhateverItHoldsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName role = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName managing = await deployment.RoleAsync([Permissions.GrantManage], cancellationToken);
        SubjectId caller = await AdministrationMemberAsync(deployment, administrative, [Permissions.SystemAdminister]);
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        var context = AccessContext.Of(caller);

        await deployment.GrantAsync(
            GrantSubject.Of(caller), managing, null, false, null, deployment.Organization, cancellationToken);

        GrantId standing = await deployment.GrantAsync(
            GrantSubject.Of(account), role, null, false, null, deployment.Organization, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IGrants grants = scope.ServiceProvider.GetRequiredService<IGrants>();
        SessionId session = await SteppedUpAsync(scope.ServiceProvider, caller);

        await deployment.RestrictAsync(caller, cancellationToken);

        Result<GrantId> granted = await grants.GrantAsync(
            context,
            session,
            OrganizationWideGrant(account, role, deployment.Organization),
            cancellationToken);
        Result revoked = await grants.RevokeAsync(context, session, standing, "No longer needed.", cancellationToken);
        Result<GrantId> unheld = await grants.GrantAsync(
            context,
            session,
            OrganizationWideGrant(account, role, new OrganizationId(Guid.NewGuid())),
            cancellationToken);

        Assert.Equal(
            [Decided.Restricted, Decided.Restricted, Decided.Restricted],
            [
                granted.Match(_ => Decided.Allowed, Refused),
                revoked.Match(() => Decided.Allowed, Refused),
                unheld.Match(_ => Decided.Allowed, Refused),
            ]);
        Assert.Equal(0, await GrantRefusalsAsync(context, deployment.Organization));
        Assert.Equal(2, await GrantsInAsync(deployment.Organization));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 AC4, AUTHZ-CONCEAL-004 (D-209): a grant on the whole of an
    /// organization the deployment does not hold, through the library's grant operation,
    /// is refused 403 <c>authz.denied</c> with one refusal recorded, naming
    /// <c>grant:manage</c> and no organization, whatever the caller holds: a holder of
    /// <c>system:administer</c> and <c>grant:manage</c> in the administrative
    /// organization, a holder of <c>grant:manage</c> there alone, and a caller holding
    /// nothing. Nothing is written.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AC4_AnOrganizationWideGrantNamingNoOrganizationIsRefusedAgainstNoneAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);
        RoleName role = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        var unheld = new OrganizationId(Guid.NewGuid());
        SubjectId[] callers =
        [
            await AdministrationMemberAsync(
                deployment,
                administrative,
                [Permissions.SystemAdminister, Permissions.GrantManage]),
            await AdministrationMemberAsync(deployment, administrative, [Permissions.GrantManage]),
            await deployment.AccountAsync(cancellationToken),
        ];

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IGrants grants = scope.ServiceProvider.GetRequiredService<IGrants>();

        foreach (SubjectId caller in callers)
        {
            Result<GrantId> refused = await grants.GrantAsync(
                AccessContext.Of(caller),
                await SteppedUpAsync(scope.ServiceProvider, caller),
                OrganizationWideGrant(account, role, unheld),
                cancellationToken);
            DeniedAccess? recorded = await RecordedRefusalAsync(scope.ServiceProvider, refused);

            Assert.Equal(1, await GrantRefusalsAsync(AccessContext.Of(caller), unheld));
            Assert.Equal(Permissions.GrantManage, recorded?.Permission);
            Assert.Null(recorded?.Organization);
        }

        await using NpgsqlConnection connection = await host.OpenAsync();

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE subject_id = @account;",
            new { account = account.Value },
            cancellationToken: cancellationToken)));
    }

    /// <summary>
    /// AUTHZ-TEST-001 AC1, AC2, AUTHZ-PRIN-001 AC1 (D-209): every case of the
    /// restrictions' table decides the way the table says through the single check, the
    /// expression and the fragment alike, a modifying permission refused
    /// <c>authz.restricted</c> by each and a reading one admitted, and none records a
    /// refusal.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(RestrictedCases))]
    public async Task AUTHZ_TEST_001_AC2_EveryRestrictedCaseDecidesTheSameWayThroughBothPathsAsync(
        string scenario,
        Decided decided)
    {
        Case written = await WriteRestrictedAsync(scenario);

        Assert.Equal(decided, await ChecksAsync(written));
        Assert.Equal(decided, await ExpressionAdmitsAsync(written));
        Assert.Equal(decided, await FragmentAdmitsAsync(written));
        Assert.Equal(0, await GrantRefusalsAsync(AccessContext.Of(written.Account), written.Deployment.Organization));
    }

    /// <summary>
    /// AUTH-STEP-002 AC10, AUTHZ-TEST-001 AC1, AC2 (D-210): every case of the requests'
    /// table decides the way the table says at both asks of one request, the second asked
    /// through the single check, the expression and the fragment alike, and a request
    /// begun after what came between the two is judged afresh and decides the other way.
    /// </summary>
    /// <param name="scenario">The case.</param>
    /// <param name="decided">What it decides.</param>
    /// <returns>The work of running it.</returns>
    [Theory]
    [MemberData(nameof(RequestCases))]
    public async Task AUTH_STEP_002_AC10_EveryRequestCaseAnswersTheSecondAskAsTheFirstThroughBothPathsAsync(
        string scenario,
        Decided decided)
    {
        Assert.Equal(decided, await AskedTwiceAsync(scenario, ChecksInAsync));
        Assert.Equal(decided, await AskedTwiceAsync(scenario, ExpressionAdmitsInAsync));
        Assert.Equal(decided, await AskedTwiceAsync(scenario, FragmentAdmitsInAsync));
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
    /// AUTHZ-DERIVE-002 AC3: the table holds the derived cases, a fact conferring a
    /// role, a deny over one and a fact on a container above, and each decides the way
    /// the table says through the check, the expression and the fragment alike.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_002_AC3_TheTableRunsItsDerivedCasesThroughBothPathsAsync()
    {
        (string Scenario, Decided Decided)[] derived =
        [
            .. Table.Where(row => row.Scenario.Contains("fact in the host's data", StringComparison.Ordinal)),
        ];
        List<(Decided Check, Decided Expression, Decided Fragment)> decided = [];

        foreach ((string scenario, Decided _) in derived)
        {
            Case written = await WriteAsync(scenario);

            decided.Add((
                await ChecksAsync(written),
                await ExpressionAdmitsAsync(written),
                await FragmentAdmitsAsync(written)));
        }

        Assert.Equal(
            [
                ("a fact in the host's data conferring a role", Decided.Allowed),
                ("a deny over a fact in the host's data", Decided.Denied),
                ("a fact in the host's data on a container above", Decided.Allowed),
            ],
            derived);
        Assert.Equal(
            derived.Select(row => (row.Decided, row.Decided, row.Decided)),
            decided);
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
        Rendered(await FilteredAsync(written, reading, deployment: null, carried: null));

    // The expression the gate renders for the case, or the refusal it answers before
    // rendering one (AUTHZ-GATE-005).
    private static async Task<Result<Expression<Func<HostDocument, bool>>>> FilteredAsync(
        Case written,
        HostContext reading,
        IServiceProvider? deployment,
        Session? carried,
        AccessContext? asking = null)
    {
        await using AsyncServiceScope scope = (deployment ?? written.Host.Services).CreateAsyncScope();

        Arrived(scope.ServiceProvider, carried);

        return await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .FilterAsync(
                asking ?? AccessContext.Of(written.Account),
                written.Asked,
                written.Record.Type,
                written.Deployment.Organization,
                Sources(reading),
                TestContext.Current.CancellationToken);
    }

    // The session a request arrived on, as the resolution stage leaves it for every
    // stage after it (BFF-ORDER-001 stage 5); a case that names none arrives on none.
    private static void Arrived(IServiceProvider request, Session? carried)
    {
        if (carried is not null)
        {
            request.GetRequiredService<RequestSession>().Resolved(carried);
        }
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

    // BFF-CAP-002 AC4, AUTHZ-GATE-005 AC6: a declaration the model refuses at the start
    // is the declaring code's fault, raised before any question is asked of it; the
    // model's refusals of any other member are no decision the case states, and fail it.
    private static Decided Started(AuthorizationDeclaration declaration, string field)
    {
        try
        {
            _ = AuthorizationModel.Of(declaration);

            return Decided.Allowed;
        }
        catch (StartupException refused) when (refused.Failure?.Code == ErrorCodes.StartupDeclarationInvalid
            && refused.Failure.Details["field"].GetString() == field)
        {
            return Decided.Raised;
        }
    }

    // The host's declaration binding system administration, one of the library's own
    // permissions, to a gate of chapter 10 section 5a or to a purpose its types declare.
    private static AuthorizationDeclaration BoundToTheLibrary(AuthorizationDeclaration declared, bool purpose) =>
        purpose
            ? declared with
            {
                ActionPurposes = new Dictionary<Permission, string>(declared.ActionPurposes)
                {
                    [Permissions.SystemAdminister] = "running the host",
                },
            }
            : declared with
            {
                StepUpGates = new Dictionary<Permission, string>(declared.StepUpGates)
                {
                    [Permissions.SystemAdminister] = "config:loosen",
                },
            };

    // The host's declaration, its document type naming as a capability beside reading a
    // document the reading of a note, which is a second permission of the action read.
    private static AuthorizationDeclaration Doubled(AuthorizationDeclaration declared) =>
        declared with
        {
            ResourceTypes =
            [
                .. declared.ResourceTypes.Select(type => type.Name == Document
                    ? type with { Capabilities = [HostPermissions.Read, HostPermissions.ReadNote] }
                    : type),
            ],
        };

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

    private async Task<Decided> ChecksAsync(
        Case written,
        IServiceProvider? deployment = null,
        Session? carried = null,
        AccessContext? asking = null) =>
        await RaisedOrAsync(async () =>
        {
            await using AsyncServiceScope scope = (deployment ?? host.Services).CreateAsyncScope();
            await using HostContext reading = host.Context();

            Arrived(scope.ServiceProvider, carried);

            Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(
                    asking ?? AccessContext.Of(written.Account),
                    written.Asked,
                    written.Record,
                    Sources(reading),
                    TestContext.Current.CancellationToken);

            return outcome.Match(() => Decided.Allowed, Refused);
        });

    private async Task<Decided> ExpressionAdmitsAsync(
        Case written,
        IServiceProvider? deployment = null,
        Session? carried = null,
        AccessContext? asking = null) =>
        await RaisedOrAsync(async () =>
        {
            await using HostContext reading = host.Context();

            return await (await FilteredAsync(written, reading, deployment, carried, asking)).Match(
                async expression => await reading.Documents
                    .Where(expression)
                    .AnyAsync(
                        document => document.Id == written.Record.Id.ToString(),
                        TestContext.Current.CancellationToken)
                    ? Decided.Allowed
                    : Decided.Denied,
                refused => Task.FromResult(Refused(refused)));
        });

    private async Task<Decided> FragmentAdmitsAsync(
        Case written,
        IServiceProvider? deployment = null,
        Session? carried = null,
        AccessContext? asking = null) =>
        await RaisedOrAsync(async () =>
        {
            Result<SqlFilter> rendered;

            await using (AsyncServiceScope scope = (deployment ?? host.Services).CreateAsyncScope())
            {
                Arrived(scope.ServiceProvider, carried);

                rendered = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                    .FragmentAsync(
                        asking ?? AccessContext.Of(written.Account),
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
        await RemovedAsync(deployment, scenario, record, inner);

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

    // AUTHZ-INHERIT-002 AC5 (D-203): the record's registration is removed by the library,
    // which revokes the grant naming it, and the same identifier is registered again
    // where it sat, so what the case decides by is whether a revoked grant comes back.
    private async Task RemovedAsync(
        Deployment deployment,
        string scenario,
        ResourceReference record,
        ResourceReference inner)
    {
        if (scenario != "a grant on a record whose registration was removed, the record registered again")
        {
            return;
        }

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            Result removed = await scope.ServiceProvider.GetRequiredService<IResources>()
                .RemoveAsync(record, TestContext.Current.CancellationToken);

            Assert.True(removed.Match(() => true, _ => false));
        }

        // The host deletes its own row with the record, and writes it again with the
        // record created again.
        await using (NpgsqlConnection connection = await host.OpenAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM host.documents WHERE id = @id;",
                new { id = record.Id.ToString() },
                cancellationToken: TestContext.Current.CancellationToken));
        }

        await deployment.RegisterAsync(record, inner, TestContext.Current.CancellationToken);
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

            // IDN-PRIN-001 (D-202): a principal naming no job the host declared is
            // refused every host permission, on its own organization's records too.
            case "a host permission asked by background work under a principal naming no declared job":
                return await JobCheckedAsync(deployment, "import");

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

            case "a grant written and then revoked, by a caller managing grants":
                return await GrantedAndRevokedAsync(deployment, role, caller);

            case "a grant written and then revoked in another organization, by a system administrator of the administrative organization":
            case "a grant written in another organization, by a system administrator restricted since the gate step":
                return await AdministeredGrantAsync(
                    deployment,
                    role,
                    restricted: scenario.EndsWith("since the gate step", StringComparison.Ordinal));

            case "a grant on the whole of an organization the deployment does not hold, by a system administrator managing grants":
            case "a grant on the whole of an organization the deployment does not hold, by a restricted system administrator":
                return await UnheldOrganizationGrantAsync(
                    deployment,
                    role,
                    restricted: scenario.EndsWith("by a restricted system administrator", StringComparison.Ordinal));

            case "a grant on a record whose registration was removed since the gate step, by a caller managing grants":
                return await RemovedRecordGrantAsync(deployment, role, caller);

            case "a member a group does not hold taken out, by a caller managing groups":
                return await MemberChangedAsync(deployment, caller, held: false);

            // CONV-DESIGN-002 (D-207): a change of members and a role's definition judge
            // their step-up before their unit of work begins, after every refusal it must
            // follow; a session that has not proved the gate is refused as before.
            case "a member added to a group, on a session that has not proved its gate, by a caller managing groups":
            case "a member taken out of a group, on a session that has not proved its gate, by a caller managing groups":
                return await MemberChangedUnprovedAsync(
                    deployment,
                    caller,
                    added: scenario.StartsWith("a member added", StringComparison.Ordinal));

            case "a role defined, on a session that has not proved its gate, by a caller managing roles":
                return await RoleDefinedUnprovedAsync(deployment, caller);

            case "a lookup of a record a fact in the host's data reaches, by a caller reading grants":
                return await ViewedAsync(deployment, caller);

            case "a fact in the host's data no grant was materialised for, after the drift check":
            case "a materialised grant the host's data no longer supports, after the drift check":
                return await DriftCheckedAsync(
                    deployment,
                    supported: scenario.StartsWith("a fact", StringComparison.Ordinal));

            case "a fact in the host's data a declared job's refresh materialised, outside a request":
                return await RefreshedByAsync(
                    deployment,
                    SystemPrincipal.ForOrganization(HostFixture.Digest, "the nightly digest", deployment.Organization),
                    inRequest: false);

            // AUTHZ-DERIVE-005 AC7 (D-208): a refresh the chapters do not admit throws and
            // writes nothing, so the fact confers nothing until a refresh they admit.
            case "a fact in the host's data a declared job's refresh was asked for, while a request is served":
                return await RefreshedByAsync(
                    deployment,
                    SystemPrincipal.ForOrganization(HostFixture.Digest, "the nightly digest", deployment.Organization),
                    inRequest: true);

            case "a fact in the host's data a principal naming no declared job asked a refresh for":
                return await RefreshedByAsync(
                    deployment,
                    SystemPrincipal.ForOrganization("import", "the nightly import", deployment.Organization),
                    inRequest: false);

            // AUTHZ-DERIVE-005 AC7, IDN-PRIN-001 AC2 (D-209): a declared job acts for its
            // own organization, so its refresh of another's record throws and writes nothing.
            case "a fact in the host's data a declared job acting for another organization asked a refresh for":
                var other = new Deployment(host);

                _ = await other.BeginAsync([HostPermissions.Read], cancellationToken);

                return await RefreshedByAsync(
                    deployment,
                    SystemPrincipal.ForOrganization(HostFixture.Digest, "the nightly digest", other.Organization),
                    inRequest: false);

            case "a fact in the host's data a reconciling principal's refresh materialised, outside a request":
            case "a fact in the host's data a reconciling principal asked a refresh for, while a request is served":
                return await RefreshedByAsync(
                    deployment,
                    SystemPrincipal.ForDeployment("mail-reconciliation", "INT-MAIL-007", SystemOperation.Reconciliation),
                    inRequest: scenario.EndsWith("while a request is served", StringComparison.Ordinal));

            case "a fact in the host's data a principal that may not reconcile asked a refresh for":
                return await RefreshedByAsync(
                    deployment,
                    SystemPrincipal.ForDeployment("retention-purge", "OPS-DATA-003", SystemOperation.RetentionPurge),
                    inRequest: false);

            case "a page asked for a declared permission its type does not list as a capability":
                return await RaisedOrAsync(async () =>
                    (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                        .CapabilitiesAsync(
                            context,
                            Document,
                            [Reference(Document).Id],
                            [HostPermissions.Export],
                            cancellationToken))
                    .Match(_ => Decided.Allowed, Refused));

            // BFF-CAP-002 AC4 (D-206): the browser reads a capability by its action, so a
            // model whose type declares two capabilities of one action never starts to
            // answer a page of it.
            case "a type declaring as capabilities two permissions of one action":
                return Started(Doubled(HostFixture.Declaration()), "capabilities");

            // AUTHZ-GATE-005 AC6 (D-209): a host binds only its own actions, so a model
            // binding a gate or a purpose to one of the library's never starts to decide
            // anything of it.
            case "a step-up gate the host bound to a library-owned permission":
                return Started(BoundToTheLibrary(HostFixture.Declaration(), purpose: false), "stepUpGate");

            case "a purpose the host bound to a library-owned permission":
                return Started(BoundToTheLibrary(HostFixture.Declaration(), purpose: true), "purpose");

            // AUTHZ-GRANT-004 AC7 (D-204): a declared role is defined by the start step
            // alone, and IRoles admits no system principal, the start's own included.
            case "a role defined with the context of the start principal role-declaration":
                return (await scope.ServiceProvider.GetRequiredService<IRoles>()
                        .DefineAsync(
                            AccessContext.Of(DeclaredRoleSeed.Principal),
                            SessionId.New(TimeProvider.System),
                            new DefinedRole(RoleName.Parse("declared"), [HostPermissions.Read]),
                            "Seeded at the start.",
                            cancellationToken))
                    .Match(_ => Decided.Allowed, Refused);

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "No such case.");
        }
    }

    // IDN-PRIN-001: a host permission asked of a record of the organization, under the
    // context of an organization-scoped principal naming the job given.
    private async Task<Decided> JobCheckedAsync(Deployment deployment, string job)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        return (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .RequireAsync(
                    AccessContext.Of(SystemPrincipal.ForOrganization(job, "the nightly digest", deployment.Organization)),
                    HostPermissions.Read,
                    record,
                    Sources(reading),
                    cancellationToken))
            .Match(() => Decided.Allowed, Refused);
    }

    // A record of the case's organization whose data subject is the case's account,
    // which the host's declared job is asked a permission on: one its declaration names,
    // the modifying one or the consent-bound one where the case says so, or one it does
    // not name; the account restricted or consenting to the consent-bound one's purpose,
    // and the organization's deletion requested or the organization erased, where the
    // case says so.
    private async Task<Case> WriteJobAsync(string scenario)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        ResourceReference outer = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference sibling = Reference(Document);

        await deployment.RegisterAsync(outer, containedIn: null, cancellationToken);
        // AUTHZ-CONCEAL-002 AC3: a record the library holds no row for is refused as one
        // the job's declaration does not reach.
        if (!scenario.EndsWith("the library holds no row for", StringComparison.Ordinal))
        {
            await deployment.RegisterAsync(record, outer, cancellationToken, subject: account);
        }

        await deployment.RegisterAsync(sibling, outer, cancellationToken);

        if (scenario.Contains("data subject is restricted", StringComparison.Ordinal))
        {
            await deployment.RestrictAsync(account, cancellationToken);
        }

        if (scenario.Contains("whose deletion is requested", StringComparison.Ordinal))
        {
            await deployment.SuspendAsync(cancellationToken);
        }

        if (scenario.Contains("once it is erased", StringComparison.Ordinal))
        {
            await deployment.EraseAsync(cancellationToken);
        }

        if (scenario.Contains("data subject consented", StringComparison.Ordinal))
        {
            await ConsentedAsync(account);
        }

        Permission asked = scenario.Contains("does not name", StringComparison.Ordinal)
            ? HostPermissions.Retain
            : scenario.Contains("consent-bound", StringComparison.Ordinal)
                ? HostPermissions.Recommend
                : scenario.Contains("modifying", StringComparison.Ordinal)
                    ? HostPermissions.Edit
                    : HostPermissions.Read;

        return new Case(host, deployment, account, record, sibling, outer, outer, asked);
    }

    // A record of the case's organization, the case's account granted on it a role
    // allowing the reading and the modifying permission save where the case names no
    // grant, and the account then restricted; the permission asked is the one the case
    // names.
    private async Task<Case> WriteRestrictedAsync(string scenario)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync([HostPermissions.Read, HostPermissions.Edit], cancellationToken);

        SubjectId account = await deployment.AccountAsync(cancellationToken);
        ResourceReference outer = Reference(Workspace);
        ResourceReference record = Reference(Document);
        ResourceReference sibling = Reference(Document);

        await deployment.RegisterAsync(outer, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, outer, cancellationToken);
        await deployment.RegisterAsync(sibling, outer, cancellationToken);

        if (!scenario.EndsWith("no grant confers", StringComparison.Ordinal))
        {
            await deployment.GrantAsync(GrantSubject.Of(account), role, record, false, null, null, cancellationToken);
        }

        await deployment.RestrictAsync(account, cancellationToken);

        Permission asked = scenario.Contains("modifying", StringComparison.Ordinal)
            ? HostPermissions.Edit
            : HostPermissions.Read;

        return new Case(host, deployment, account, record, sibling, outer, outer, asked);
    }

    // The declared job's context, acting for the case's organization or, where the case
    // says so, for another organization the deployment holds.
    private async Task<AccessContext> JobOfAsync(Case written, string scenario)
    {
        OrganizationId acting = written.Deployment.Organization;

        if (scenario.Contains("another organization", StringComparison.Ordinal))
        {
            var other = new Deployment(host);

            _ = await other.BeginAsync([HostPermissions.Read], TestContext.Current.CancellationToken);

            acting = other.Organization;
        }

        return AccessContext.Of(SystemPrincipal.ForOrganization(HostFixture.Digest, "the nightly digest", acting));
    }

    // The check, the expression and the fragment asked under the declared job's context
    // in background work, each list decided by whether it holds the record.
    private async Task<(Decided Check, Decided Expression, Decided Fragment)> JobListedAsync(
        Case written,
        string scenario)
    {
        AccessContext job = await JobOfAsync(written, scenario);

        return (
            await ChecksAsync(written, asking: job),
            await ExpressionAdmitsAsync(written, asking: job),
            await FragmentAdmitsAsync(written, asking: job));
    }

    // The check, the expression and the fragment asked under the declared job's context
    // from the scope given; a rendering handed out rather than refused is a list admitted.
    private async Task<(Decided Check, Decided Expression, Decided Fragment)> JobAskedAsync(
        IServiceProvider asking,
        Case written)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IAccessGate gate = asking.GetRequiredService<IAccessGate>();
        var job = AccessContext.Of(SystemPrincipal.ForOrganization(
            HostFixture.Digest,
            "the nightly digest",
            written.Deployment.Organization));

        await using HostContext reading = host.Context();

        Decided check = (await gate.RequireAsync(job, written.Asked, written.Record, Sources(reading), cancellationToken))
            .Match(() => Decided.Allowed, Refused);
        Decided expression = (await gate.FilterAsync(
                job,
                written.Asked,
                written.Record.Type,
                written.Deployment.Organization,
                Sources(reading),
                cancellationToken))
            .Match(_ => Decided.Allowed, Refused);
        Decided fragment = (await gate.FragmentAsync(
                job,
                written.Asked,
                written.Record.Type,
                written.Deployment.Organization,
                "identity_authz_row",
                "id",
                cancellationToken))
            .Match(_ => Decided.Allowed, Refused);

        return (check, expression, fragment);
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

    // AUTHZ-DERIVE-005 AC7 (D-207, D-208): a record under a workspace someone reviews, in
    // the deployment with the derivation materialised, and a refresh the system principal
    // given calls, outside a request or while one is served. A refresh the chapters admit
    // writes the grant, which confers what any refresh's does; one they do not throws,
    // which is the case's expected fault, and writes nothing, so the fact confers nothing.
    private async Task<Decided> RefreshedByAsync(Deployment deployment, SystemPrincipal principal, bool inRequest)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);
        SubjectId reviewing = await deployment.AccountAsync(cancellationToken);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.NamedRoleAsync(RoleName.Parse("reviewer"), [HostPermissions.Read], cancellationToken);
        await deployment.ReviewAsync(workspace, reviewing, cancellationToken);

        async Task RefreshAsync(IServiceProvider services)
        {
            await using HostContext reading = host.Context();

            IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

            await work.BeginAsync(CancellationToken.None);

            Result<DerivationRefresh>? answered = null;

            try
            {
                answered = await services.GetRequiredService<IDerivationMaterialiser>().RefreshAsync(
                    AccessContext.Of(principal),
                    "reviewer",
                    workspace.Id,
                    Sources(reading),
                    CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
                return;
            }

            Rendered(answered.Value);

            await work.CommitAsync(CancellationToken.None);
        }

        await using ServiceProvider materialised = Materialised();

        if (inRequest)
        {
            await using ServedRequests served = await ServedRequests.StartAsync(
                host,
                HostFixture.Declaration(materialised: true),
                async context => await RefreshAsync(context.RequestServices),
                cancellationToken);

            _ = await served.SendAsync(ServedRequests.Browser);
        }
        else
        {
            await using AsyncServiceScope refreshing = materialised.CreateAsyncScope();

            await RefreshAsync(refreshing.ServiceProvider);
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
        Session session = Begun(
            caller,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            Deployment.Noon - TimeSpan.FromMinutes(1));

        await KeptAsync(services, session);

        return session.Id;
    }

    private static Session Begun(SubjectId caller, Assurance reached, DateTimeOffset at) =>
        Session.Begin(
            SessionId.New(TimeProvider.System),
            caller,
            reached,
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            at,
            TimeSpan.FromDays(7),
            TimeSpan.FromDays(30),
            breakGlassReason: null);

    // The session as the store keeps it, which is where the gate reads it from.
    private static async Task KeptAsync(IServiceProvider services, Session session)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(cancellationToken);

        // A session is kept under its person's key, which an account written directly
        // does not have until its first session asks for it.
        ISubjectKeyStore keys = services.GetRequiredService<ISubjectKeyStore>();

        if (await keys.FindBySubjectAsync(session.Subject, cancellationToken) is null)
        {
            await keys.CreateAsync(session.Subject, cancellationToken);
        }

        await services.GetRequiredService<ISessionStore>().AddAsync(
            session,
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32),
            cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    // What the session of each case of the sessions' table last reached, and when,
    // against the strict gate: two factors, phishing-resistant, five minutes old at
    // most. Each case leaves unmet the one thing its row names and nothing else, but
    // the case of delegated alone, which reaches nothing the gate asks: a gate whose
    // level is delegated asks no maximum age (AUTH-STEP-007), no gate bound to an
    // action states that level (AUTH-STEP-002a), and so what such a session reached
    // passes none of them however lately. The request arrives on the last session a
    // case answers; a derived session comes after the record it stands on
    // (AUTH-SESS-012).
    private static IReadOnlyList<Session> Proving(SubjectId caller, string scenario)
    {
        DateTimeOffset recent = Deployment.Noon - TimeSpan.FromMinutes(1);
        DateTimeOffset aged = Deployment.Noon - TimeSpan.FromMinutes(6);
        var met = new Assurance(AssuranceLevel.Aal2, PhishingResistant: true);
        var lower = new Assurance(AssuranceLevel.Aal1, PhishingResistant: true);

        switch (scenario)
        {
            case "a session that meets the gate":
                return [Begun(caller, met, recent)];

            case "a session below the level the gate asks":
                return [Begun(caller, lower, recent)];

            case "a session that was not phishing-resistant, at a gate asking it":
                return [Begun(caller, met with { PhishingResistant = false }, recent)];

            case "a session whose proof is older than the gate's maximum age":
                return [Begun(caller, met, aged)];

            case "a session that reached a lower level since, beside the gate's level past its maximum age":
                Session renewed = Begun(caller, met, aged);

                renewed.Present(lower, recent);

                return [renewed];

            case "a session whose proof was reached only before its last downgrade":
                Session downgraded = Begun(caller, met, recent);

                downgraded.Downgrade(Deployment.Noon - TimeSpan.FromSeconds(30));

                return [downgraded];

            case "a session that reached delegated alone, the level whose own gate asks no maximum age":
                return [Begun(caller, new Assurance(AssuranceLevel.Delegated, PhishingResistant: false), recent)];

            case "a session derived from a record that meets the gate":
                Session standing = Begun(caller, met, recent);

                return [standing, Derived(standing, Deployment.Noon - TimeSpan.FromSeconds(30))];

            case "a session derived within the gate's maximum age from a record whose proof is older than it":
                Session old = Begun(caller, met, aged);

                return [old, Derived(old, recent)];

            case "a session derived after its record's last downgrade, the record's proof reached only before it":
                Session lowered = Begun(caller, met, Deployment.Noon - TimeSpan.FromMinutes(3));

                lowered.Downgrade(Deployment.Noon - TimeSpan.FromMinutes(2));

                return [lowered, Derived(lowered, recent)];

            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "The table has no such case.");
        }
    }

    // The session another application establishes from a record (AUTH-SESS-012).
    private static Session Derived(Session record, DateTimeOffset at) =>
        record.Derive(
            SessionId.New(TimeProvider.System),
            SessionType.PerApp,
            new SessionOrigin("198.51.100.7", new DeviceDescription("Firefox", "Linux")),
            at,
            TimeSpan.FromDays(7));

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

    // AUTHZ-GROUP-001, OPS-CFG-007: a member added to a group, or one the group holds
    // taken out, by a caller managing groups on a session the store holds no record of,
    // which has proved no gate; the change writes nothing whatever it is refused.
    private async Task<Decided> MemberChangedUnprovedAsync(Deployment deployment, SubjectId caller, bool added)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var member = GrantSubject.Of(await deployment.AccountAsync(cancellationToken));
        var context = AccessContext.Of(caller);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;
        IGroups groups = services.GetRequiredService<IGroups>();

        GroupId group = (await groups.CreateAsync(
                context,
                deployment.Organization,
                "Reviewers",
                "A group for the reviewers.",
                cancellationToken))
            .Match(created => created, error => throw new InvalidOperationException(error.Code.ToString()));

        if (!added)
        {
            (await groups.AddMemberAsync(
                    context,
                    await SteppedUpAsync(services, caller),
                    group,
                    member,
                    "Joined the reviewers.",
                    cancellationToken))
                .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
        }

        var unproved = SessionId.New(TimeProvider.System);
        Decided decided = (added
                ? await groups.AddMemberAsync(context, unproved, group, member, "Joined the reviewers.", cancellationToken)
                : await groups.RemoveMemberAsync(context, unproved, group, member, "Left the reviewers.", cancellationToken))
            .Match(() => Decided.Allowed, Refused);

        await using NpgsqlConnection connection = await host.OpenAsync();

        int members = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM identity.group_members WHERE group_id = @group;",
            new { group = group.Value },
            cancellationToken: cancellationToken));

        Assert.Equal(added ? 0 : 1, members);

        return decided;
    }

    // AUTHZ-GRANT-004, OPS-CFG-007: a role defined by a member of the administrative
    // organization managing roles there, on a session the store holds no record of,
    // which has proved no gate.
    private async Task<Decided> RoleDefinedUnprovedAsync(Deployment deployment, SubjectId caller)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName managing = await deployment.RoleAsync([Permissions.RoleManage], cancellationToken);

        await deployment.MemberAsync(caller, administrative, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(caller), managing, null, false, null, administrative, cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        return (await scope.ServiceProvider.GetRequiredService<IRoles>()
                .DefineAsync(
                    AccessContext.Of(caller),
                    SessionId.New(TimeProvider.System),
                    new DefinedRole(RoleName.Parse("drafter"), [HostPermissions.Read]),
                    "A role for the drafters.",
                    cancellationToken))
            .Match(_ => Decided.Allowed, Refused);
    }

    // AUTHZ-GRANT-001, AUTHZ-GRANT-003 AC6 (D-206): a grant across the organization to
    // another account, written and revoked by a caller managing grants there on a session
    // that meets the gate. Recording each in the trail decides nothing, so the row
    // restates the decision the two operations make.
    private async Task<Decided> GrantedAndRevokedAsync(Deployment deployment, RoleName role, SubjectId caller)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        var context = AccessContext.Of(caller);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IServiceProvider services = scope.ServiceProvider;
        SessionId session = await SteppedUpAsync(services, caller);
        IGrants grants = services.GetRequiredService<IGrants>();

        Result<GrantId> written = await grants.GrantAsync(
            context,
            session,
            new GrantRequest(
                GrantSubject.Of(account),
                role,
                new ResourceReference(
                    ResourceType.Parse("organization"),
                    ResourceId.Parse(deployment.Organization.ToString())),
                Deny: false,
                ExpiresAt: null,
                "The reason the grant was asked for."),
            cancellationToken);

        if (written.Match<Error?>(_ => null, error => error) is Error refused)
        {
            return Refused(refused);
        }

        return (await grants.RevokeAsync(
                context,
                session,
                written.Match(grant => grant, _ => default),
                "No longer needed.",
                cancellationToken))
            .Match(() => Decided.Allowed, Refused);
    }

    // AUTHZ-SCOPE-001 AC4, AUTHZ-GATE-006 AC3 (D-207, D-208): a grant across an
    // organization other than the administrative organization, written and revoked
    // through the library's operations by a member of the administrative organization
    // holding system:administer there and nothing in the grant's, on a session that meets
    // the gate. Each operation asks the admission at its gate step and again inside its
    // unit of work, and neither ask records a refusal. Where the case restricts the caller
    // between the two, the grant is refused there, records nothing and writes nothing.
    private async Task<Decided> AdministeredGrantAsync(Deployment deployment, RoleName role, bool restricted)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName administering = await deployment.RoleAsync([Permissions.SystemAdminister], cancellationToken);
        SubjectId caller = await deployment.AccountAsync(cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        var context = AccessContext.Of(caller);

        await deployment.MemberAsync(caller, administrative, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(caller), administering, null, false, null, administrative, cancellationToken);

        // The session is kept by the deployment that reads it, under its own keys, before
        // anything is interleaved.
        bool kept = false;

        await using ServiceProvider interleaved = Interleaved(
            async cancelled =>
            {
                if (kept && restricted)
                {
                    await deployment.RestrictAsync(caller, cancelled);
                }
            });

        SessionId session;

        await using (AsyncServiceScope keeping = interleaved.CreateAsyncScope())
        {
            session = await SteppedUpAsync(keeping.ServiceProvider, caller);
            kept = true;
        }

        await using AsyncServiceScope scope = interleaved.CreateAsyncScope();

        IGrants grants = scope.ServiceProvider.GetRequiredService<IGrants>();

        Result<GrantId> written = await grants.GrantAsync(
            context,
            session,
            new GrantRequest(
                GrantSubject.Of(account),
                role,
                new ResourceReference(
                    ResourceType.Parse("organization"),
                    ResourceId.Parse(deployment.Organization.ToString())),
                Deny: false,
                ExpiresAt: null,
                "The organization's first grant."),
            cancellationToken);

        Decided decided = await written.Match(
            async grant => (await grants.RevokeAsync(context, session, grant, "No longer needed.", cancellationToken))
                .Match(() => Decided.Allowed, Refused),
            refused => Task.FromResult(Refused(refused)));

        await using NpgsqlConnection connection = await host.OpenAsync();

        int held = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE subject_id = @account;",
            new { account = account.Value },
            cancellationToken: cancellationToken));

        Assert.Equal(0, await GrantRefusalsAsync(context, deployment.Organization));
        Assert.Equal(restricted ? 0 : 1, held);
        Assert.False(Assert.IsType<UnitOfWorkInterleaved>(scope.ServiceProvider.GetRequiredService<IUnitOfWork>()).Open);

        return decided;
    }

    // AUTHZ-SCOPE-001 AC4, AUTHZ-CONCEAL-004 (D-209): a grant on the whole of an
    // organization the deployment holds no row for, asked through the library's operation
    // by a member of the administrative organization holding system:administer and
    // grant:manage there. It belongs to no organization, so neither permission is
    // decided: the restriction is judged first, recording nothing, and otherwise one
    // refusal of grant:manage is recorded against no organization. Nothing is written.
    private async Task<Decided> UnheldOrganizationGrantAsync(Deployment deployment, RoleName role, bool restricted)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        RoleName administering = await deployment.RoleAsync(
            [Permissions.SystemAdminister, Permissions.GrantManage],
            cancellationToken);
        SubjectId caller = await deployment.AccountAsync(cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        var context = AccessContext.Of(caller);
        var unheld = new OrganizationId(Guid.NewGuid());

        await deployment.MemberAsync(caller, administrative, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(caller), administering, null, false, null, administrative, cancellationToken);

        if (restricted)
        {
            await deployment.RestrictAsync(caller, cancellationToken);
        }

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result<GrantId> written = await scope.ServiceProvider.GetRequiredService<IGrants>().GrantAsync(
            context,
            SessionId.New(TimeProvider.System),
            new GrantRequest(
                GrantSubject.Of(account),
                role,
                new ResourceReference(ResourceType.Parse("organization"), ResourceId.Parse(unheld.ToString())),
                Deny: false,
                ExpiresAt: null,
                "A grant on an organization no row names."),
            cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        int held = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE subject_id = @account;",
            new { account = account.Value },
            cancellationToken: cancellationToken));

        Assert.Equal(0, held);
        Assert.Equal(restricted ? 0 : 1, await GrantRefusalsAsync(context, unheld));

        if (written.Match<Error?>(_ => null, error => error) is Error refusal && refusal.Code == ErrorCodes.Denied)
        {
            DeniedAccess? recorded = await scope.ServiceProvider.GetRequiredService<IAccessAudit>().FindAsync(
                new AuditRecordId(refusal.Details["correlation"].GetGuid()),
                cancellationToken);

            Assert.Equal(Permissions.GrantManage, recorded?.Permission);
            Assert.Null(recorded?.Organization);
        }

        return written.Match(_ => Decided.Allowed, Refused);
    }

    // AUTHZ-SCOPE-001, AUTHZ-CONCEAL-004 (09 section 8): a grant on a record, asked through
    // the library's operation by a caller managing grants in the record's organization,
    // on a session that meets the gate, whose registration another connection removes
    // between the gate step and the unit of work. The record then belongs to no
    // organization, so it is refused as the gate step refuses one no registration names:
    // one refusal of grant:manage recorded against no organization, carried in
    // details.correlation. Nothing is written and no unit of work is left open.
    private async Task<Decided> RemovedRecordGrantAsync(Deployment deployment, RoleName role, SubjectId caller)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SubjectId account = await deployment.AccountAsync(cancellationToken);
        ResourceReference record = Reference(Workspace);
        var context = AccessContext.Of(caller);

        await deployment.RegisterAsync(record, containedIn: null, cancellationToken);

        bool kept = false;

        await using ServiceProvider interleaved = Interleaved(
            async cancelled =>
            {
                if (kept)
                {
                    await using NpgsqlConnection removing = await host.OpenAsync();

                    await removing.ExecuteAsync(new CommandDefinition(
                        """
                        DELETE FROM identity.ancestry WHERE resource_type = @type AND resource_id = @id;
                        DELETE FROM identity.resources WHERE resource_type = @type AND resource_id = @id;
                        """,
                        new { type = record.Type.ToString(), id = record.Id.ToString() },
                        cancellationToken: cancelled));
                }
            });

        SessionId session;

        await using (AsyncServiceScope keeping = interleaved.CreateAsyncScope())
        {
            session = await SteppedUpAsync(keeping.ServiceProvider, caller);
            kept = true;
        }

        await using AsyncServiceScope scope = interleaved.CreateAsyncScope();

        Result<GrantId> written = await scope.ServiceProvider.GetRequiredService<IGrants>().GrantAsync(
            context,
            session,
            new GrantRequest(
                GrantSubject.Of(account),
                role,
                record,
                Deny: false,
                ExpiresAt: null,
                "A grant on a record removed meanwhile."),
            cancellationToken);

        await using NpgsqlConnection connection = await host.OpenAsync();

        int held = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE subject_id = @account;",
            new { account = account.Value },
            cancellationToken: cancellationToken));

        Assert.Equal(0, held);
        Assert.Equal(1, await GrantRefusalsAsync(context, deployment.Organization));
        Assert.False(Assert.IsType<UnitOfWorkInterleaved>(scope.ServiceProvider.GetRequiredService<IUnitOfWork>()).Open);

        Error refusal = written.Match(_ => throw new InvalidOperationException("The grant was written."), error => error);
        DeniedAccess? recorded = await scope.ServiceProvider.GetRequiredService<IAccessAudit>().FindAsync(
            new AuditRecordId(refusal.Details["correlation"].GetGuid()),
            cancellationToken);

        Assert.Equal(Permissions.GrantManage, recorded?.Permission);
        Assert.Null(recorded?.Organization);

        return Refused(refusal);
    }

    // A member of the administrative organization holding there a role that allows what
    // the case names, and nothing anywhere else.
    private static async Task<SubjectId> AdministrationMemberAsync(
        Deployment deployment,
        OrganizationId administrative,
        IReadOnlyList<Permission> permissions)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        RoleName held = await deployment.RoleAsync(permissions, cancellationToken);
        SubjectId member = await deployment.AccountAsync(cancellationToken);

        await deployment.MemberAsync(member, administrative, cancellationToken);
        await deployment.GrantAsync(GrantSubject.Of(member), held, null, false, null, administrative, cancellationToken);

        return member;
    }

    // AUTHZ-GRANT-001 AC2: a grant of the role to an account on the whole of an
    // organization, named by its identifier.
    private static GrantRequest OrganizationWideGrant(SubjectId account, RoleName role, OrganizationId organization) =>
        new(
            GrantSubject.Of(account),
            role,
            new ResourceReference(ResourceType.Parse("organization"), ResourceId.Parse(organization.ToString())),
            Deny: false,
            ExpiresAt: null,
            "A grant on the whole of the organization.");

    // How many grants an organization holds, revoked ones among them.
    private async Task<int> GrantsInAsync(OrganizationId organization)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM identity.grants WHERE organization = @organization;",
            new { organization = organization.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // AUTHZ-CONCEAL-004: the refusal record an authz.denied resolves to by the
    // identifier its details.correlation carries.
    private static async Task<DeniedAccess?> RecordedRefusalAsync(IServiceProvider services, Result<GrantId> refused)
    {
        Error refusal = refused.Match(_ => throw new InvalidOperationException("The grant was written."), error => error);

        Assert.Equal(ErrorCodes.Denied, refusal.Code);

        return await services.GetRequiredService<IAccessAudit>().FindAsync(
            new AuditRecordId(refusal.Details["correlation"].GetGuid()),
            TestContext.Current.CancellationToken);
    }

    // AUTHZ-SCOPE-001 (D-207, D-208): the caller of a grant admission's case and the
    // organization its grant is in, a new one other than the administrative organization
    // save where the case names that. A system administrator holds system:administer in
    // the administrative organization as a member of it, save where the case says it
    // holds no membership.
    private async Task<(AccessContext Context, OrganizationId Organization)> AdmittingAsync(string scenario)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);

        OrganizationId administrative = await deployment.AdministrativeAsync(cancellationToken);
        OrganizationId organization = scenario.StartsWith("a grant in the administrative organization", StringComparison.Ordinal)
            ? administrative
            : deployment.Organization;

        if (scenario == "a grant in another organization, by background work")
        {
            return (
                AccessContext.Of(SystemPrincipal.ForOrganization("import", "the nightly import", organization)),
                organization);
        }

        RoleName managing = await deployment.RoleAsync([Permissions.GrantManage], cancellationToken);
        RoleName administering = await deployment.RoleAsync([Permissions.SystemAdminister], cancellationToken);
        SubjectId caller = await deployment.AccountAsync(cancellationToken);
        var holder = GrantSubject.Of(caller);

        if (!scenario.EndsWith("holding no membership of the administrative organization", StringComparison.Ordinal))
        {
            await deployment.MemberAsync(caller, administrative, cancellationToken);
        }

        if (scenario.Contains("system administrator", StringComparison.Ordinal))
        {
            await deployment.GrantAsync(holder, administering, null, false, null, administrative, cancellationToken);
        }

        if (scenario.EndsWith("managing grants there", StringComparison.Ordinal))
        {
            await deployment.GrantAsync(holder, managing, null, false, null, organization, cancellationToken);
        }

        if (scenario.EndsWith("denied grant management there", StringComparison.Ordinal))
        {
            await deployment.GrantAsync(holder, managing, null, true, null, organization, cancellationToken);
        }

        if (scenario.EndsWith("in the administrative organization alone", StringComparison.Ordinal))
        {
            await deployment.GrantAsync(holder, managing, null, false, null, administrative, cancellationToken);
        }

        if (scenario.Contains("by a restricted", StringComparison.Ordinal))
        {
            await deployment.RestrictAsync(caller, cancellationToken);
        }

        return (AccessContext.Of(caller), organization);
    }

    // AUTHZ-CONCEAL-004: the refusals a case's caller left recorded: a person's wherever
    // they are, and background work's, which names the nil subject, against the grant's
    // organization.
    private async Task<int> GrantRefusalsAsync(AccessContext context, OrganizationId organization)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.denied'
              AND acting_subject = @acting
              AND (@person OR organization = @organization);
            """,
            new
            {
                organization = organization.Value,
                acting = context.Acting?.Value ?? Guid.Empty,
                person = context.Acting is not null,
            },
            cancellationToken: TestContext.Current.CancellationToken));
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

        Session session = Begun(
            caller,
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            Deployment.Noon - ago);

        if (downgraded)
        {
            session.Downgrade(Deployment.Noon - (ago / 2));
        }

        await KeptAsync(services, session);

        Arrived(services, session);

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
            case "a grant on a record whose type declares its encrypted field under a name of its own":
            case "a grant on a record whose type no other type's declaration shares a name with":
            case "a grant on a record whose type declares no two encrypted fields under one name":
            case "a grant whose role does not allow the permission":
            case "a grant on a record whose registration was removed, the record registered again":
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

    // One request of a case of the requests' table on a deployment whose clock moves:
    // the first ask through the single check, then what the case puts between the two
    // asks, then the second through the path given, both in the request's one scope. A
    // request begun afterwards is judged afresh and decides the other way, which shows
    // that what came between would have changed the judgement.
    private async Task<Decided> AskedTwiceAsync(
        string scenario,
        Func<IServiceProvider, Case, HostContext, Task<Decided>> again)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Case written = await WriteBoundAsync();
        var clock = new FixedClock(Deployment.Noon);
        DateTimeOffset recent = Deployment.Noon - TimeSpan.FromMinutes(1);
        DateTimeOffset aged = Deployment.Noon - TimeSpan.FromMinutes(6);
        bool met = scenario.Contains(" that met the gate", StringComparison.Ordinal);
        AssuranceProviderInMemory? provider = scenario.StartsWith("a report", StringComparison.Ordinal)
            ? new AssuranceProviderInMemory(Proved(met ? recent : aged))
            : null;
        Session? carried = provider is null
            ? Begun(written.Account, new Assurance(AssuranceLevel.Aal2, PhishingResistant: true), met ? recent : aged)
            : null;

        await using ServiceProvider deployment = Requesting(clock, provider);

        if (carried is not null)
        {
            await using AsyncServiceScope keeping = deployment.CreateAsyncScope();

            await KeptAsync(keeping.ServiceProvider, carried);
        }

        Decided first;
        Decided second;

        await using (AsyncServiceScope request = deployment.CreateAsyncScope())
        {
            await using HostContext reading = host.Context();

            Arrived(request.ServiceProvider, carried);

            first = await ChecksInAsync(request.ServiceProvider, written, reading);

            if (met)
            {
                clock.Advance(TimeSpan.FromMinutes(10));
            }
            else if (provider is not null)
            {
                provider.Reports(Proved(Deployment.Noon));
            }
            else if (carried is not null)
            {
                await LiftedAsync(deployment, carried.Id, cancellationToken);
            }

            second = await again(request.ServiceProvider, written, reading);
        }

        Decided afresh = await ChecksAsync(written, deployment, carried);

        Assert.Equal(first, second);
        Assert.NotEqual(first, afresh);

        return second;
    }

    // What a host reports of a proof that reached the strict gate at the instant given.
    private static AttainedAssurance Proved(DateTimeOffset at) =>
        new(Aal1At: at, Aal2At: at, Aal3At: null, PhishingResistantAt: at, AssuranceLevel.Aal2);

    // What another request's step-up writes into the session record: the gate's level,
    // phishing-resistant, reached now (AUTH-SESS-001).
    private static async Task LiftedAsync(IServiceProvider deployment, SessionId lifted, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope stepping = deployment.CreateAsyncScope();

        IServiceProvider services = stepping.ServiceProvider;
        IUnitOfWork work = services.GetRequiredService<IUnitOfWork>();
        ISessionStore store = services.GetRequiredService<ISessionStore>();

        await work.BeginAsync(cancellationToken);

        Session session = await store.FindAsync(lifted, cancellationToken)
            ?? throw new InvalidOperationException("The session is not kept.");

        session.Present(
            new Assurance(AssuranceLevel.Aal2, PhishingResistant: true),
            services.GetRequiredService<TimeProvider>().GetUtcNow());

        await store.RecordAsync(session, cancellationToken);
        await work.CommitAsync(cancellationToken);
    }

    // The same deployment on a clock the case moves, with a host's assurance provider
    // registered where the case judges from its report.
    private ServiceProvider Requesting(FixedClock clock, IAssuranceProvider? provider)
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));

        if (provider is not null)
        {
            services.AddSingleton(provider);
        }

        services.AddJanus(host.ConnectionString, HostFixture.Declaration(), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // The single check asked in the scope of a request already under way.
    private static async Task<Decided> ChecksInAsync(IServiceProvider request, Case written, HostContext reading) =>
        (await request.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(written.Account),
                written.Asked,
                written.Record,
                Sources(reading),
                TestContext.Current.CancellationToken))
        .Match(() => Decided.Allowed, Refused);

    // The expression asked in the scope of a request already under way.
    private static async Task<Decided> ExpressionAdmitsInAsync(IServiceProvider request, Case written, HostContext reading) =>
        await (await request.GetRequiredService<IAccessGate>()
            .FilterAsync(
                AccessContext.Of(written.Account),
                written.Asked,
                written.Record.Type,
                written.Deployment.Organization,
                Sources(reading),
                TestContext.Current.CancellationToken))
        .Match(
            async expression => await reading.Documents
                .Where(expression)
                .AnyAsync(
                    document => document.Id == written.Record.Id.ToString(),
                    TestContext.Current.CancellationToken)
                ? Decided.Allowed
                : Decided.Denied,
            refused => Task.FromResult(Refused(refused)));

    // The fragment asked in the scope of a request already under way.
    private async Task<Decided> FragmentAdmitsInAsync(IServiceProvider request, Case written, HostContext reading)
    {
        Result<SqlFilter> rendered = await request.GetRequiredService<IAccessGate>()
            .FragmentAsync(
                AccessContext.Of(written.Account),
                written.Asked,
                written.Record.Type,
                written.Deployment.Organization,
                "identity_authz_row",
                "id",
                TestContext.Current.CancellationToken);

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
