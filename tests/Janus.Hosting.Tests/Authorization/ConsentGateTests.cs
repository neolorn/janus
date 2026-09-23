using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Consents;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What a consent decides at the gate: an action done for a purpose resting on
/// consent runs only where the subject consented to that purpose, and an action done
/// for another purpose on the same record is untouched by it
/// (PRIV-SENS-002, PRIV-SENS-002a, PRIV-SENS-003, AUTHZ-GATE-005).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConsentGateTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Recommendations = "recommendations";

    private static readonly ResourceType Document = ResourceType.Parse("document");

    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// PRIV-SENS-002 AC1, PRIV-SENS-003 AC2: the grant is there and the consent is
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
    /// PRIV-SENS-002a AC1, AC3, PRIV-SENS-003 AC3: the same record is read under the
    /// purpose resting on the contract whether the consent-based one was ever given,
    /// withdrawn, or never asked for.
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
    /// PRIV-SENS-003 AC1: the type the host declares sensitive derives the written
    /// requirement from the declaration alone, so an ordinary consent over it is the
    /// wrong kind and a written one admits the action.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_003_AC1_TheSensitiveDeclarationDerivesTheWrittenRequirementAsync()
    {
        Granted granted = await GrantedAsync();

        await RecordAsync(granted.Account, Held(ConsentKind.Ordinary));

        Assert.Equal(ErrorCodes.ConsentWrittenRequired, await RefusalAsync(granted));

        await RecordAsync(granted.Account, Held(ConsentKind.Written));

        Assert.Null(await RefusalAsync(granted));
    }

    /// <summary>
    /// PRIV-SENS-003 AC2: the written record is asked for by the consent-based
    /// purpose and by nothing else, so reading the same sensitive record for a
    /// purpose resting on the contract is admitted with no consent anywhere.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_003_AC2_TheWrittenRecordIsAskedForByTheConsentBasedPurposeOnlyAsync()
    {
        Granted granted = await GrantedAsync();

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(granted));
        Assert.Null(await RefusalAsync(granted, HostPermissions.Read));
    }

    /// <summary>
    /// PRIV-SENS-003 AC3: a customer who has granted no consent-based purpose still
    /// has their record read and kept, and the capability offers the reading without
    /// a residual rather than withholding it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_003_AC3_TheRecordIsReadForACustomerWhoConsentedToNothingAsync()
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

        var acting = new Granted(staff, record);

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

        var acting = new Granted(account, record);

        await RecordAsync(account, Held(ConsentKind.Written));

        Assert.Equal(ErrorCodes.ConsentRequired, await RefusalAsync(acting));
        Assert.Null(await RefusalAsync(acting, HostPermissions.Read));
    }

    private static ConsentRecord Held(ConsentKind kind) =>
        new(
            Recommendations,
            "1",
            ConsentMechanism.Dashboard,
            kind,
            Deployment.Noon,
            WithdrawnAt: null,
            SupersededAt: null);

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private async Task<Granted> GrantedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            [HostPermissions.Read, HostPermissions.Recommend],
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

        return new Granted(account, record);
    }

    private async Task RecordAsync(SubjectId subject, ConsentRecord consent)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<IConsentStore>()
            .RecordAsync(subject, consent, TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);
    }

    // The type a document sits in declares a derivation, so the check is asked with the
    // rows that derivation is evaluated over (AUTHZ-DERIVE-001, D-162).
    private async Task<ErrorCode?> RefusalAsync(Granted granted, Permission? permission = null)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
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

    private static FilterSources<HostDocument> Sources(HostContext reading) =>
        new FilterSources<HostDocument>(reading.Ancestry, reading.Grants, document => document.Id)
            .Relationship("reviewer", reading.Reviewers);

    // One case's rows: the account holding the grant and the record it holds it on.
    private sealed record Granted(SubjectId Account, ResourceReference Record);
}
