using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Hosting.Bff;
using Janus.Privacy.Consents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What a consent decides at the gate: an action done for a purpose resting on
/// consent runs only where the subject consented to that purpose, and an action done
/// for another purpose on the same record is untouched by it
/// (PRIV-SENS-001, PRIV-SENS-002, PRIV-SENS-002a, AUTHZ-GATE-005).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConsentGateTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Recommendations = "recommendations";

    private static readonly ResourceType Document = ResourceType.Parse("document");

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// PRIV-SENS-002 AC1: the grant is there and the consent is
    /// not, so the action done for the consent-based purpose over the sensitive type
    /// is refused by the code that names what is missing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC1_AConsentBasedPurposeWithoutAConsentIsRefusedAsync()
    {
        Granted granted = await GrantedAsync();

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-SENS-002 AC1, PRIV-CONS-004 AC1: the written record is what a sensitive
    /// type's consent-based purpose asks for, and an ordinary one is refused as the
    /// wrong kind rather than accepted as good enough.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC1_AnOrdinaryConsentOverASensitiveTypeIsRefusedAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(granted.Account, Held(ConsentKind.Ordinary));

        Assert.Equal(ErrorCodes.ConsentWrittenRequired, await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-SENS-002 AC1: with the written consent recorded and live, the action the
    /// grants confer runs.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC1_AWrittenConsentAdmitsTheActionAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(granted.Account, Held(ConsentKind.Written));

        Assert.Null(await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-CONS-007 AC3, AC4: a consent a material revision ended prompts for
    /// re-consent rather than reading as one never given.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC4_ASupersededConsentIsRefusedAsSupersededAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { SupersededAt = Deployment.Noon.AddDays(30) });

        Assert.Equal(ErrorCodes.ConsentSuperseded, await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-CONS-007 AC2, AC4, AC5, AUTHZ-GATE-002 AC4: a deployment whose declaration
    /// gives the purpose another governing document refuses the consent recorded against
    /// the earlier one as superseded, in the check and in both renderings of the list
    /// alike, interrupts nothing resting on another basis, and admits the action again
    /// once the subject consents against the document the purpose now names.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_APurposeGivenAnotherDocumentAsksItsSubjectsAgainAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(granted.Account, Held(ConsentKind.Written));

        await using ServiceProvider redeclared = Redeclared("recommendation-terms");

        Assert.Null(await RefusalAsync(granted));
        Assert.True(await ListedAsync(granted, host.Services));
        Assert.True(await ListedByFragmentAsync(granted, host.Services));

        Assert.Equal(ErrorCodes.ConsentSuperseded, await RefusalAsync(granted, deployment: redeclared));
        Assert.False(await ListedAsync(granted, redeclared));
        Assert.False(await ListedByFragmentAsync(granted, redeclared));
        Assert.Null(await RefusalAsync(granted, HostPermissions.Read, redeclared));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { Document = "recommendation-terms", GrantedAt = Deployment.Noon.AddDays(1) });

        Assert.Null(await RefusalAsync(granted, deployment: redeclared));
        Assert.True(await ListedAsync(granted, redeclared));
        Assert.True(await ListedByFragmentAsync(granted, redeclared));
    }

    /// <summary>
    /// PRIV-CONS-007 AC3: a material revision of the notice ends the consent-based
    /// purpose and nothing else, so the record is still read under the purpose
    /// resting on the contract.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_007_AC3_ARevisedNoticeInterruptsNoContractualProcessingAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { SupersededAt = Deployment.Noon.AddDays(30) });

        Assert.Null(await RefusalAsync(granted, HostPermissions.Read));
        Assert.Equal(ErrorCodes.ConsentSuperseded, await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-CONS-008 AC4, PRIV-SENS-002a AC2: a withdrawn consent stops the purpose
    /// on the next request, the record staying where it is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002a_AC2_WithdrawingStopsThePurposeOnTheNextRequestAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(granted.Account, Held(ConsentKind.Written));

        Assert.Null(await RefusalAsync(granted));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { WithdrawnAt = Deployment.Noon.AddDays(1) });

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-CONS-001 AC4, AUTHZ-GATE-005 AC3: the record the gate reads is the live one,
    /// so a consent given again after a withdrawal admits the action and leaves the
    /// capability no residual, the withdrawn record standing beside it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC4_AConsentGivenAgainAfterAWithdrawalAdmitsTheActionAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { WithdrawnAt = Deployment.Noon.AddDays(1) });

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { GrantedAt = Deployment.Noon.AddDays(2) });

        Assert.Null(await RefusalAsync(granted));
        Assert.Empty(await RequiredAsync(granted));
    }

    /// <summary>
    /// PRIV-CONS-001 AC4, PRIV-CONS-007 AC4: where no record is live the gate reads the
    /// latest, so a consent given after a supersession and then withdrawn is refused as
    /// required, and not as the superseded one before it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_CONS_001_AC4_WhereNoRecordIsLiveTheLatestDecidesTheRefusalAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { SupersededAt = Deployment.Noon.AddDays(1) });

        Assert.Equal(ErrorCodes.ConsentSuperseded, await RefusalAsync(granted));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { GrantedAt = Deployment.Noon.AddDays(2) });

        Assert.Null(await RefusalAsync(granted));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { WithdrawnAt = Deployment.Noon.AddDays(3) });

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));
        Assert.Equal(
            [CapabilityResidual.Consent],
            (await RequiredAsync(granted))[HostPermissions.Recommend]);
    }

    /// <summary>
    /// PRIV-SENS-002a AC1: the same record is read under the purpose resting on the
    /// contract whether the consent-based one was ever given, withdrawn, or never
    /// asked for.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002a_AC1_AnotherPurposeOnTheSameRecordIsUntouchedAsync()
    {
        Granted granted = await GrantedAsync();

        Assert.Null(await RefusalAsync(granted, HostPermissions.Read));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { WithdrawnAt = Deployment.Noon.AddDays(1) });

        Assert.Null(await RefusalAsync(granted, HostPermissions.Read));
        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-SENS-002a AC3: the subject's own record, kept as the books a legal
    /// obligation has the host keep, is still read by them for that purpose once they
    /// withdraw the purpose resting on consent, which alone stops.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002a_AC3_TheBooksStayVisibleToTheirSubjectAfterAWithdrawalAsync()
    {
        Granted granted = await GrantedAsync(HostPermissions.Retain);

        await RecordAsync(granted.Account, Held(ConsentKind.Written));

        Assert.Null(await RefusalAsync(granted, HostPermissions.Retain));
        Assert.Null(await RefusalAsync(granted));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { WithdrawnAt = Deployment.Noon.AddDays(1) });

        Assert.Null(await RefusalAsync(granted, HostPermissions.Retain));
        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));
    }

    /// <summary>
    /// AUTHZ-GATE-005 AC3: the capability is offered with the consent it still
    /// requires, so the control prompts rather than failing silently, and the consent
    /// recorded takes the residual away.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_005_AC3_ACapabilityCarriesTheConsentItStillRequiresAsync()
    {
        Granted granted = await GrantedAsync();

        Assert.Equal(
            [CapabilityResidual.Consent],
            (await RequiredAsync(granted))[HostPermissions.Recommend]);

        await RecordAsync(granted.Account, Held(ConsentKind.Written));

        Assert.Empty(await RequiredAsync(granted));
    }

    /// <summary>
    /// PRIV-SENS-001 AC1: the type the host declares sensitive derives the written
    /// requirement from the declaration alone, so an ordinary consent over it is the
    /// wrong kind and a written one admits the action.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_001_AC1_TheSensitiveDeclarationDerivesTheWrittenRequirementAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(granted.Account, Held(ConsentKind.Ordinary));

        Assert.Equal(ErrorCodes.ConsentWrittenRequired, await RefusalAsync(granted));

        await RecordAsync(granted.Account, Held(ConsentKind.Written));

        Assert.Null(await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-SENS-002 AC1: the written record is asked for by the consent-based
    /// purpose and by nothing else, so reading the same sensitive record for a
    /// purpose resting on the contract is admitted with no consent anywhere.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC1_TheWrittenRecordIsAskedForByTheConsentBasedPurposeOnlyAsync()
    {
        Granted granted = await GrantedAsync();

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));
        Assert.Null(await RefusalAsync(granted, HostPermissions.Read));
    }

    /// <summary>
    /// PRIV-SENS-002a AC1: a subject who has granted no consent-based purpose still
    /// has their record read and kept, and the capability offers the reading without
    /// a residual rather than withholding it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002a_AC1_TheRecordIsReadForASubjectWhoConsentedToNothingAsync()
    {
        Granted granted = await GrantedAsync();

        IReadOnlyDictionary<Permission, IReadOnlySet<CapabilityResidual>> residual =
            await RequiredAsync(granted);

        Assert.Null(await RefusalAsync(granted, HostPermissions.Read));
        Assert.DoesNotContain(HostPermissions.Read, residual);
        Assert.Equal([CapabilityResidual.Consent], residual[HostPermissions.Recommend]);
    }

    /// <summary>
    /// PRIV-SENS-002 AC1: without the data subject's recorded written consent, whoever
    /// the caller is. A member of staff acting on a customer's record is admitted by
    /// the customer's consent and by nothing the staff member consented to.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC1_StaffAreGatedByTheRecordsSubjectsConsentAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend],
            cancellationToken);
        SubjectId customer = await deployment.AccountAsync(cancellationToken);
        SubjectId staff = await deployment.AccountAsync(cancellationToken);

        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken, customer);
        await deployment.GrantAsync(
            GrantSubject.Of(staff),
            role,
            workspace,
            false,
            null,
            null,
            cancellationToken);

        var acting = new Granted(staff, record, deployment.Organization);

        await RecordAsync(staff, Held(ConsentKind.Written));

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(acting));

        await RecordAsync(customer, Held(ConsentKind.Written));

        Assert.Null(await RefusalAsync(acting));
    }

    /// <summary>
    /// PRIV-SENS-002 AC1: a record naming no data subject has nobody's consent to
    /// read, so the consent-based purpose is refused on it however much the caller
    /// consented to for themselves.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC1_ARecordNamingNoSubjectAdmitsNoConsentedActionAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend],
            cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);

        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(account),
            role,
            workspace,
            false,
            null,
            null,
            cancellationToken);

        var acting = new Granted(account, record, deployment.Organization);

        await RecordAsync(account, Held(ConsentKind.Written));

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(acting));
        Assert.Null(await RefusalAsync(acting, HostPermissions.Read));
    }

    /// <summary>
    /// AUTHZ-GATE-005 AC1, PRIV-SENS-002 AC1: a page whose records belong to twelve
    /// data subjects reads their consents in one query, so it costs the statements a
    /// page of one record costs, and each record still carries the residual its own
    /// subject's consent leaves.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_005_AC1_APageReadsTheConsentsOfEverySubjectOnItOnceAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend],
            cancellationToken);
        SubjectId staff = await deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.GrantAsync(
            GrantSubject.Of(staff),
            role,
            workspace,
            false,
            null,
            null,
            cancellationToken);

        List<ResourceId> page = [];
        HashSet<ResourceId> consented = [];

        for (int each = 0; each < 12; each++)
        {
            SubjectId customer = await deployment.AccountAsync(cancellationToken);
            ResourceReference record = Reference(Document);

            await deployment.RegisterAsync(record, workspace, cancellationToken, customer);
            page.Add(record.Id);

            if (each % 2 == 0)
            {
                await RecordAsync(customer, Held(ConsentKind.Written));
                _ = consented.Add(record.Id);
            }
        }

        (IReadOnlyList<Capability> one, int single) = await CountedPageAsync(staff, [page[0]]);
        (IReadOnlyList<Capability> twelve, int whole) = await CountedPageAsync(staff, page);

        Assert.Empty(Assert.Single(one).Requires);
        Assert.Equal(single, whole);
        Assert.All(twelve, capability => Assert.Equal(
            !consented.Contains(capability.Resource),
            capability.Requires.TryGetValue(HostPermissions.Recommend, out IReadOnlySet<CapabilityResidual>? requires)
                && requires.SetEquals([CapabilityResidual.Consent])));
    }

    private static ConsentRecord Held(ConsentKind kind) =>
        new(
            Recommendations,
            "privacy-notice",
            "1",
            ConsentMechanism.Dashboard,
            kind,
            Deployment.Noon,
            WithdrawnAt: null,
            SupersededAt: null);

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private async Task<Granted> GrantedAsync(params Permission[] also)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend, .. also],
            cancellationToken);
        SubjectId account = await deployment.AccountAsync(cancellationToken);

        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);

        // PRIV-SENS-002 AC1: the consent the gate reads is the record's data subject's,
        // which the host writes from the column its type declares for its encrypted
        // fields.
        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken, account);
        await deployment.GrantAsync(
            GrantSubject.Of(account),
            role,
            workspace,
            false,
            null,
            null,
            cancellationToken);

        return new Granted(account, record, deployment.Organization);
    }

    private async Task RecordAsync(SubjectId subject, ConsentRecord consent)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        IConsentStore store = scope.ServiceProvider.GetRequiredService<IConsentStore>();

        await work.BeginAsync(cancellationToken);

        // PRIV-CONS-001: a live record replaces the live one that stands, and a record
        // that ended is the standing one stamped, or one added and then stamped.
        if (consent.Live)
        {
            _ = await store.SupersedeAsync(subject, consent.Purpose, consent.GrantedAt, cancellationToken);
        }

        _ = await store.AddAsync(
            subject,
            consent with { WithdrawnAt = null, SupersededAt = null },
            cancellationToken);

        if (consent.SupersededAt is { } superseded)
        {
            _ = await store.SupersedeAsync(subject, consent.Purpose, superseded, cancellationToken);
        }

        if (consent.WithdrawnAt is { } withdrawn)
        {
            _ = await store.WithdrawConsentAsync(subject, consent.Purpose, withdrawn, cancellationToken);
        }

        await work.CommitAsync(cancellationToken);
    }

    // The type a document sits in declares a derivation, so the check is asked with the
    // rows that derivation is evaluated over (AUTHZ-DERIVE-001, D-162).
    private async Task<ErrorCode?> RefusalAsync(
        Granted granted,
        Permission? permission = null,
        IServiceProvider? deployment = null)
    {
        await using AsyncServiceScope scope = (deployment ?? host.Services).CreateAsyncScope();
        await using HostContext reading = host.Context();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(granted.Account),
                permission ?? HostPermissions.Recommend,
                granted.Record,
                Sources(reading),
                TestContext.Current.CancellationToken);

        return outcome.Match(() => (ErrorCode?)null, error => error.Code);
    }

    private async Task<IReadOnlyDictionary<Permission, IReadOnlySet<CapabilityResidual>>>
        RequiredAsync(Granted granted)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        Result<IReadOnlyList<Capability>> answered = await scope.ServiceProvider
            .GetRequiredService<IAccessGate>()
            .CapabilitiesAsync(
                AccessContext.Of(granted.Account),
                Document,
                [granted.Record.Id],
                [HostPermissions.Read, HostPermissions.Recommend],
                Sources(reading),
                TestContext.Current.CancellationToken);

        return answered.Match(
            capabilities => capabilities[0].Requires,
            error => throw new InvalidOperationException(error.Code.ToString()));
    }

    private async Task<(IReadOnlyList<Capability> Page, int Statements)> CountedPageAsync(
        SubjectId account,
        IReadOnlyList<ResourceId> page)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        using var traced = new TracedStatements();

        Result<IReadOnlyList<Capability>> answered = await gate.CapabilitiesAsync(
            AccessContext.Of(account),
            Document,
            page,
            [HostPermissions.Read, HostPermissions.Recommend],
            Sources(reading),
            TestContext.Current.CancellationToken);

        return (
            answered.Match(
                capabilities => capabilities,
                error => throw new InvalidOperationException(error.Code.ToString())),
            traced.Statements);
    }

    // The same deployment under a declaration that gives the recommendations purpose
    // another governing document, which is what a release that moves a purpose is.
    private ServiceProvider Redeclared(string document)
    {
        var services = new ServiceCollection();

        services.AddSingleton<TimeProvider>(new FixedTime(Deployment.Noon));
        services.AddSingleton<ISecretSource>(HostFixture.Secrets(host.MaintenanceConnectionString));
        HostFixture.Sourced(services, host.ConnectionString);
        services.AddJanus(host.ConnectionString, HostFixture.Declaration(document: document), ApplicationKind.Public);

        return HostFixture.Started(services.BuildServiceProvider());
    }

    // Whether the list for the consent-bound action holds the record, through the
    // expression composed into the host's own query.
    private async Task<bool> ListedAsync(Granted granted, IServiceProvider deployment)
    {
        await using AsyncServiceScope scope = deployment.CreateAsyncScope();
        await using HostContext reading = host.Context();

        Result<Expression<Func<HostDocument, bool>>> filter = await scope.ServiceProvider
            .GetRequiredService<IAccessGate>()
            .FilterAsync(
                AccessContext.Of(granted.Account),
                HostPermissions.Recommend,
                Document,
                granted.Organization,
                Sources(reading),
                TestContext.Current.CancellationToken);

        return await reading.Documents
            .Where(filter.Match(
                rendering => rendering,
                error => throw new InvalidOperationException(error.Code.ToString())))
            .AnyAsync(
                document => document.Id == granted.Record.Id.ToString(),
                TestContext.Current.CancellationToken);
    }

    // The same through the fragment, composed into a hand-written query.
    private async Task<bool> ListedByFragmentAsync(Granted granted, IServiceProvider deployment)
    {
        Result<SqlFilter> rendered;

        await using (AsyncServiceScope scope = deployment.CreateAsyncScope())
        {
            rendered = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .FragmentAsync(
                    AccessContext.Of(granted.Account),
                    HostPermissions.Recommend,
                    Document,
                    granted.Organization,
                    "identity_authz_row",
                    "id",
                    TestContext.Current.CancellationToken);
        }

        SqlFilter fragment = rendered.Match(
            rendering => rendering,
            error => throw new InvalidOperationException(error.Code.ToString()));

        var arguments = new DynamicParameters();

        foreach (KeyValuePair<string, object> parameter in fragment.Parameters)
        {
            arguments.Add(parameter.Key, parameter.Value);
        }

        arguments.Add("record", granted.Record.Id.ToString());

        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM host.documents AS identity_authz_row "
            + "WHERE identity_authz_row.id = @record AND " + fragment.Text + ");",
            arguments,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, reading.Consented, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    // One case's rows: the account holding the grant and the record it holds it on.
    private sealed record Granted(SubjectId Account, ResourceReference Record, OrganizationId Organization);
}
