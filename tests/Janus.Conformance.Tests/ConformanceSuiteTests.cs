using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Xunit;

namespace Janus.Conformance.Tests;

/// <summary>
/// The conformance suite run as a host runs it, against the sample host that declares
/// only the values it has to (LIB-TEST-001, AUTH-OIDC-006).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConformanceSuiteTests(SampleHost host) : IClassFixture<SampleHost>
{
    // AUTHZ-TEST-001: every scenario the declaration places a shelf in, which is at
    // the top of its chain and derives the keeper role from its own facts.
    private static readonly TruthTableCase[] Shelves =
    [
        new(TruthTableScenario.GrantOnRecord, SampleHost.ReadShelf, Allowed: true),
        new(TruthTableScenario.GrantOnOrganization, SampleHost.ReadShelf, Allowed: true),
        new(TruthTableScenario.GrantOnSibling, SampleHost.ReadShelf, Allowed: false),
        new(TruthTableScenario.NoGrant, SampleHost.ReadShelf, Allowed: false),
        new(TruthTableScenario.GrantToGroup, SampleHost.ReadShelf, Allowed: true),
        new(TruthTableScenario.GrantToNestedGroup, SampleHost.ReadShelf, Allowed: true),
        new(TruthTableScenario.DenyOverGrant, SampleHost.ReadShelf, Allowed: false),
        new(TruthTableScenario.ExpiredGrant, SampleHost.ReadShelf, Allowed: false),
        new(TruthTableScenario.RevokedGrant, SampleHost.ReadShelf, Allowed: false),
        new(TruthTableScenario.GrantInAnotherOrganization, SampleHost.ReadShelf, Allowed: false),
        new(TruthTableScenario.RoleWithoutPermission, SampleHost.ReadShelf, Allowed: false),
        new(TruthTableScenario.DerivedGrant, SampleHost.ReadShelf, Allowed: true),
        new(TruthTableScenario.DenyOverDerivedGrant, SampleHost.ReadShelf, Allowed: false),
    ];

    // A binder sits on a shelf and derives the steward role from its own facts, so the
    // container cases and a role derived on the container join the table.
    private static readonly TruthTableCase[] Binders =
    [
        new(TruthTableScenario.GrantOnRecord, SampleHost.ReadBinder, Allowed: true),
        new(TruthTableScenario.GrantOnContainer, SampleHost.ReadBinder, Allowed: true),
        new(TruthTableScenario.GrantOnOrganization, SampleHost.ReadBinder, Allowed: true),
        new(TruthTableScenario.GrantOnSibling, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.NoGrant, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.GrantToGroup, SampleHost.ReadBinder, Allowed: true),
        new(TruthTableScenario.GrantToNestedGroup, SampleHost.ReadBinder, Allowed: true),
        new(TruthTableScenario.DenyOverGrant, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.DenyOnContainerOverGrant, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.ExpiredGrant, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.RevokedGrant, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.GrantInAnotherOrganization, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.RoleWithoutPermission, SampleHost.ReadBinder, Allowed: false),
        new(TruthTableScenario.DerivedGrant, SampleHost.ReadBinder, Allowed: true),
        new(TruthTableScenario.DerivedGrantOnContainer, SampleHost.ReadBinder, Allowed: true),
        new(TruthTableScenario.DenyOverDerivedGrant, SampleHost.ReadBinder, Allowed: false),
    ];

    // A sheet is filed in a binder on a shelf and derives nothing of its own, so it is
    // reached through two levels and through the role derived on its binder.
    private static readonly TruthTableCase[] Sheets =
    [
        new(TruthTableScenario.GrantOnRecord, SampleHost.ReadSheet, Allowed: true),
        new(TruthTableScenario.GrantOnContainer, SampleHost.ReadSheet, Allowed: true),
        new(TruthTableScenario.GrantAboveContainer, SampleHost.ReadSheet, Allowed: true),
        new(TruthTableScenario.GrantOnOrganization, SampleHost.ReadSheet, Allowed: true),
        new(TruthTableScenario.GrantOnSibling, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.NoGrant, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.GrantToGroup, SampleHost.ReadSheet, Allowed: true),
        new(TruthTableScenario.GrantToNestedGroup, SampleHost.ReadSheet, Allowed: true),
        new(TruthTableScenario.DenyOverGrant, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.DenyOnContainerOverGrant, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.ExpiredGrant, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.RevokedGrant, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.GrantInAnotherOrganization, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.RoleWithoutPermission, SampleHost.ReadSheet, Allowed: false),
        new(TruthTableScenario.DerivedGrantOnContainer, SampleHost.ReadSheet, Allowed: true),
        new(TruthTableScenario.DenyOverDerivedGrant, SampleHost.ReadSheet, Allowed: false),
    ];

    // The purpose every declaration below rests its types on, which holds together.
    private const string Keeping = "keeping records";

    /// <summary>
    /// LIB-TEST-001 AC1: every entity the sample host maps is a declared type, the rows
    /// of a declared relationship or a contract table.
    /// </summary>
    [Fact]
    public void LIB_TEST_001_AC1_EveryEntityTheSampleHostMapsHasAPolicy()
    {
        using SampleContext context = host.Context();

        ConformanceReport report = ConformanceSuite.Policies(host.Services, context.Model);

        Assert.Empty(report.Findings);
        Assert.True(report.Conforms);
    }

    /// <summary>
    /// LIB-TEST-001 AC1: an entity the host maps and declares nothing about is found,
    /// named, under the code of an unregistered policy.
    /// </summary>
    [Fact]
    public void LIB_TEST_001_AC1_AnEntityWithoutAPolicyIsFound()
    {
        using StrayContext context = host.Stray();

        ConformanceReport report = ConformanceSuite.Policies(host.Services, context.Model);

        ConformanceFinding found = Assert.Single(report.Findings);
        Assert.False(report.Conforms);
        Assert.Equal(ConformanceCheck.Policies, found.Check);
        Assert.Equal(ErrorCodes.PolicyUnregistered, found.Failure.Code);
        Assert.Equal(typeof(Label).FullName, found.Failure.Details["entity"].GetString());
    }

    /// <summary>
    /// LIB-TEST-001 AC2: every case of the shelf's table is decided as the table states,
    /// by the single check and by the list filter alike.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_001_AC2_EveryShelfCaseAgreesThroughCheckAndFilterAsync()
    {
        ConformanceReport report = await TableAsync(
            context => new SampleRows<Shelf>(context, SampleHost.ShelfType, row => row.Id),
            Shelves);

        Assert.Empty(report.Findings);
    }

    /// <summary>
    /// LIB-TEST-001 AC2: every case of the binder's table is decided as the table
    /// states, by the single check and by the list filter alike.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_001_AC2_EveryBinderCaseAgreesThroughCheckAndFilterAsync()
    {
        ConformanceReport report = await TableAsync(
            context => new SampleRows<Binder>(context, SampleHost.BinderType, row => row.Id),
            Binders);

        Assert.Empty(report.Findings);
    }

    /// <summary>
    /// LIB-TEST-001 AC2: every case of the sheet's table is decided as the table states,
    /// by the single check and by the list filter alike.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_001_AC2_EverySheetCaseAgreesThroughCheckAndFilterAsync()
    {
        ConformanceReport report = await TableAsync(
            context => new SampleRows<Sheet>(context, SampleHost.SheetType, row => row.Id),
            Sheets);

        Assert.Empty(report.Findings);
    }

    /// <summary>
    /// LIB-TEST-001 AC2: a case the gate decides otherwise than the table states is
    /// reported with what each path decided.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task LIB_TEST_001_AC2_ACaseDecidedOtherwiseThanTheTableStatesIsReportedAsync()
    {
        ConformanceReport report = await TableAsync(
            context => new SampleRows<Sheet>(context, SampleHost.SheetType, row => row.Id),
            [new(TruthTableScenario.NoGrant, SampleHost.ReadSheet, Allowed: true)]);

        ConformanceFinding found = Assert.Single(report.Findings);
        Assert.Equal(ConformanceCheck.TruthTable, found.Check);
        Assert.Equal(ErrorCodes.TruthTableDisagreement, found.Failure.Code);
        Assert.Equal(
            ("sheet", "no-grant", "sheet:read", true, false, false),
            (found.Failure.Details["type"].GetString(),
                found.Failure.Details["scenario"].GetString(),
                found.Failure.Details["permission"].GetString(),
                found.Failure.Details["expected"].GetBoolean(),
                found.Failure.Details["check"].GetBoolean(),
                found.Failure.Details["filter"].GetBoolean()));
    }

    /// <summary>
    /// LIB-TEST-001 AC3: the sample host's declaration holds together.
    /// </summary>
    [Fact]
    public void LIB_TEST_001_AC3_TheSampleDeclarationHoldsTogether()
    {
        ConformanceReport report = ConformanceSuite.Declaration(SampleHost.Declaration());

        Assert.Empty(report.Findings);
    }

    /// <summary>
    /// LIB-TEST-001 AC3: each way a declaration fails to hold together is reported under
    /// its own code, naming what is at fault.
    /// </summary>
    [Fact]
    public void LIB_TEST_001_AC3_EachMalformedDeclarationIsReportedByItsOwnCode()
    {
        (ErrorCode Code, string Member, string Value)[] reported =
        [
            .. new Action<AuthorizationDeclarationBuilder>[]
            {
                declaring => declaring
                    .Resource<Binder>("binder", type => Kept(type.ContainedIn("sheet")))
                    .Resource<Sheet>("sheet", type => Kept(type.ContainedIn("binder"))),
                declaring => declaring
                    .Resource<Binder>("binder", type => Kept(type.ContainedIn("drawer"))),
                declaring => declaring
                    .Resource<Binder>("binder", type => Kept(type)),
                declaring => declaring
                    .Resource<Binder>("binder", type => type
                        .BelongsToOrganization()
                        .Purpose("improving", "legitimate-interest", data: ["records"], subjects: ["members"])),
                declaring => declaring
                    .Resource<Binder>("binder", type => Kept(type.BelongsToOrganization().Derivation("custodian", "custodian"))),
                declaring => declaring
                    .Resource<Binder>("binder", type => type
                        .BelongsToOrganization()
                        .Purpose(Keeping, "contractual-obligation", subjects: ["members"])),
            }.Select(fault => Reported(ConformanceSuite.Declaration(Declared(fault)))),
        ];

        Assert.Equal(
            [
                (ErrorCodes.StartupContainmentCycle, "type", "binder"),
                (ErrorCodes.StartupUndeclaredTypeReference, "type", "binder"),
                (ErrorCodes.StartupNoOrganizationPath, "type", "binder"),
                (ErrorCodes.StartupMissingAssessment, "purpose", "improving"),
                (ErrorCodes.StartupUndeclaredDerivationReference, "type", "binder"),
                (ErrorCodes.StartupDeclarationMissing, "key", Keeping),
            ],
            reported);
        Assert.Equal(reported.Length, reported.Select(each => each.Code).Distinct().Count());
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1: the sample host's provider refuses every form the two
    /// specifications retire, asked over its own endpoints under the host's prefix.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC1_TheSampleHostsProviderRefusesEveryRetiredFormAsync()
    {
        using HttpClient client = host.Client();

        ConformanceReport report = await ConformanceSuite.ProviderAsync(
            client,
            SampleHost.Issuer,
            host.Registered,
            TestContext.Current.CancellationToken);

        Assert.Empty(report.Findings);
    }

    /// <summary>
    /// AUTH-OIDC-006 AC1: a provider that admits and advertises what it should refuse
    /// is reported once for each form, naming what was sent and what came back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_OIDC_006_AC1_AProviderAdmittingWhatItShouldRefuseIsReportedAsync()
    {
        using var provider = new AdmittingProvider();
        using var client = new HttpClient(provider, disposeHandler: false);

        ConformanceReport report = await ConformanceSuite.ProviderAsync(
            client,
            AdmittingProvider.Issuer,
            host.Registered,
            TestContext.Current.CancellationToken);

        Assert.Equal(20, report.Findings.Count);
        Assert.All(report.Findings, found => Assert.Equal(ErrorCodes.ProviderNonconformant, found.Failure.Code));
        Assert.Contains(
            ("push", "response_type", "token", "unsupported_response_type", 200),
            report.Findings.Where(found => found.Failure.Details.ContainsKey("field")).Select(found => (
                found.Failure.Details["probe"].GetString(),
                found.Failure.Details["field"].GetString(),
                found.Failure.Details["sent"].GetString(),
                found.Failure.Details["expected"].GetString(),
                found.Failure.Details["status"].GetInt32())));
        Assert.Equal(
            [
                "response_types_supported",
                "code_challenge_methods_supported",
                "grant_types_supported",
                "require_pushed_authorization_requests",
                "token_endpoint_auth_methods_supported",
            ],
            report.Findings
                .Where(found => found.Failure.Details.ContainsKey("member"))
                .Select(found => found.Failure.Details["member"].GetString()));
    }

    // A declaration that holds together but for what the case adds to it.
    private static AuthorizationDeclaration Declared(Action<AuthorizationDeclarationBuilder> alongside)
    {
        var declaring = new AuthorizationDeclarationBuilder();

        foreach (LawfulBasisDeclaration basis in LawfulBases.Default)
        {
            _ = declaring.LawfulBasis(basis);
        }

        _ = declaring
            .RetentionFloor("records", TimeSpan.FromDays(730))
            .Permission(SampleHost.ReadShelf.ToString())
            .Resource<Shelf>("shelf", type => Kept(type.BelongsToOrganization()));

        alongside(declaring);

        return declaring.Build();
    }

    private static ResourceTypeDeclarationBuilder<TResource> Kept<TResource>(ResourceTypeDeclarationBuilder<TResource> type) =>
        type.Purpose(Keeping, "contractual-obligation", data: ["records"], subjects: ["members"]);

    private static (ErrorCode Code, string Member, string Value) Reported(ConformanceReport report)
    {
        ConformanceFinding found = Assert.Single(report.Findings);
        KeyValuePair<string, JsonElement> named = Assert.Single(found.Failure.Details);

        Assert.Equal(ConformanceCheck.Declaration, found.Check);

        return (found.Failure.Code, named.Key, named.Value.GetString() ?? string.Empty);
    }

    private async Task<ConformanceReport> TableAsync<TResource>(
        Func<SampleContext, SampleRows<TResource>> rows,
        IReadOnlyList<TruthTableCase> cases)
        where TResource : class
    {
        await using SampleContext context = host.Context();

        return await ConformanceSuite.TruthTableAsync(
            host.Services,
            host.ConnectAsync,
            rows(context),
            cases,
            TestContext.Current.CancellationToken);
    }
}
