using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Core.Configuration;
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
    /// PRIV-SENS-002 AC5: the list for a consent-bound action, as the expression and as
    /// the fragment alike, holds the record only while its data subject's consent is
    /// live, of the written kind the sensitive type asks for, and recorded against the
    /// document the purpose names. No consent, an ordinary one, a superseded one, a
    /// withdrawn one and a live one against another document each admit nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC5_AListAdmitsOnlyALiveConsentOfTheKindAgainstTheNamedDocumentAsync()
    {
        Granted granted = await GrantedAsync();

        await using ServiceProvider redeclared = Redeclared("recommendation-terms");

        Assert.Equal((false, false), await ListedBothWaysAsync(granted, host.Services));

        await RecordAsync(granted.Account, Held(ConsentKind.Ordinary));

        Assert.Equal((false, false), await ListedBothWaysAsync(granted, host.Services));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { GrantedAt = Deployment.Noon.AddDays(1) });

        Assert.Equal((true, true), await ListedBothWaysAsync(granted, host.Services));
        Assert.Equal((false, false), await ListedBothWaysAsync(granted, redeclared));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with
            {
                GrantedAt = Deployment.Noon.AddDays(2),
                SupersededAt = Deployment.Noon.AddDays(3),
            });

        Assert.Equal((false, false), await ListedBothWaysAsync(granted, host.Services));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with { GrantedAt = Deployment.Noon.AddDays(4) });

        Assert.Equal((true, true), await ListedBothWaysAsync(granted, host.Services));

        await RecordAsync(
            granted.Account,
            Held(ConsentKind.Written) with
            {
                GrantedAt = Deployment.Noon.AddDays(5),
                WithdrawnAt = Deployment.Noon.AddDays(6),
            });

        Assert.Equal((false, false), await ListedBothWaysAsync(granted, host.Services));
    }

    /// <summary>
    /// PRIV-SENS-002 AC6: a consent-based purpose is admitted on a data subject's
    /// consent and on nothing else, so a check that names no record, asked by an
    /// account holding the permission over the whole organization and a written
    /// consent of its own, is refused as a check on a record with no subject is.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task PRIV_SENS_002_AC6_ACheckNamingNoRecordOrARecordWithNoSubjectIsRefusedAsync()
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
            on: null,
            false,
            null,
            null,
            cancellationToken);
        await RecordAsync(account, Held(ConsentKind.Written));

        var acting = new Granted(account, record, deployment.Organization);

        Assert.Equal(
            ErrorCodes.ConsentRequired,
            await RefusalAsync(account, HostPermissions.Recommend, deployment.Organization));
        Assert.Null(await RefusalAsync(account, HostPermissions.Read, deployment.Organization));
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

    /// <summary>
    /// IDN-PRIN-001 AC6, AUTHZ-GATE-005, AUTHZ-CONCEAL-004 AC5, OPS-OBS-001 (D-207,
    /// D-208): a declared job asking a consent-bound permission its declaration names, of
    /// its organization, is refused <c>privacy.consent.required</c> as a person is,
    /// carrying no refusal record's identifier, writing no refusal record and so counting
    /// nothing toward the denial alert.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC6_AJobsConsentRefusalOnTheOrganizationWideCheckWritesNoRecordAsync()
    {
        Jobbed jobbed = await JobbedAsync(consented: true);

        Error refused;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            refused = (await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                    .RequireAsync(
                        jobbed.Job,
                        HostPermissions.Recommend,
                        jobbed.Written.Organization,
                        TestContext.Current.CancellationToken))
                .Match(() => throw new InvalidOperationException("The permission was admitted."), error => error);
        }

        Assert.Equal(ErrorCodes.ConsentRequired, refused.Code);
        Assert.False(refused.Details.ContainsKey("correlation"));
        Assert.Equal(0, await JobDenialsAsync(jobbed.Written.Organization));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004 AC5, IDN-PRIN-001 AC6, PRIV-SENS-002 AC1 (D-208): a declared
    /// job asking a consent-bound permission its declaration names, on a record whose
    /// data subject has not consented, is refused <c>privacy.consent.required</c> as a
    /// person is, carrying no refusal record's identifier, writing no refusal record, and
    /// the lists leave the record out.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AC5_AJobsConsentRefusalOnARecordCarriesNoIdentifierAndWritesNoRecordAsync()
    {
        Jobbed jobbed = await JobbedAsync(consented: false);

        Error? refused = await JobRefusalAsync(jobbed);

        Assert.Equal(ErrorCodes.ConsentRequired, refused?.Code);
        Assert.False(refused?.Details.ContainsKey("correlation"));
        Assert.Equal(0, await JobDenialsAsync(jobbed.Written.Organization));
        Assert.Equal(
            (false, false),
            await ListedBothWaysAsync(jobbed.Written, host.Services, jobbed.Job));
    }

    /// <summary>
    /// IDN-PRIN-001 AC6, PRIV-SENS-002 AC1 (D-207, D-208): a declared job asking a
    /// consent-bound permission its declaration names, on a record of its organization
    /// whose data subject holds the written consent the purpose asks, is admitted through
    /// the check and both renderings of the list.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_PRIN_001_AC6_AJobIsAdmittedOnAConsentBoundPermissionWhereConsentIsHeldAsync()
    {
        Jobbed jobbed = await JobbedAsync(consented: true);

        Assert.Null(await JobRefusalAsync(jobbed));
        Assert.Equal(
            (true, true),
            await ListedBothWaysAsync(jobbed.Written, host.Services, jobbed.Job));
    }

    /// <summary>
    /// AUTHZ-CONCEAL-004 AC5, PRIV-SENS-002 AC1, PRIV-CONS-007 AC4 (D-208): a person's
    /// consent-bound action refused <c>privacy.consent.required</c>,
    /// <c>privacy.consent.writtenrequired</c> or <c>privacy.consent.superseded</c>, on a
    /// record and on the organization, carries no refusal record's identifier, writes no
    /// refusal record, and so counts nothing toward <c>alerting.denials.threshold</c>,
    /// however often it is asked.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CONCEAL_004_AC5_APersonsConsentRefusalCarriesNoIdentifierAndWritesNoRecordAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Granted none = await GrantedAsync();
        Granted ordinary = await GrantedAsync();
        Granted superseded = await GrantedAsync();

        // PRIV-SENS-002 AC6: the check naming no record is asked by an account holding the
        // permission over the whole organization.
        var deployment = new Deployment(host);
        RoleName role = await deployment.BeginAsync([HostPermissions.Read, HostPermissions.Recommend], cancellationToken);
        SubjectId wide = await deployment.AccountAsync(cancellationToken);

        await deployment.GrantAsync(GrantSubject.Of(wide), role, on: null, false, null, null, cancellationToken);
        await RecordAsync(ordinary.Account, Held(ConsentKind.Ordinary));
        await RecordAsync(
            superseded.Account,
            Held(ConsentKind.Written) with { SupersededAt = Deployment.Noon.AddDays(30) });

        var refusals = new List<Error?>();

        for (int each = 0; each <= Settings.AlertingDenialsThreshold.Default; each++)
        {
            refusals.Add(await RefusedWithAsync(none));
        }

        Error? organizationWide = await RefusedWithAsync(wide, HostPermissions.Recommend, deployment.Organization);
        Error? notWritten = await RefusedWithAsync(ordinary);
        Error? ended = await RefusedWithAsync(superseded);

        Assert.All(refusals.Append(organizationWide), refusal => Assert.Equal(ErrorCodes.ConsentRequired, refusal?.Code));
        Assert.Equal(ErrorCodes.ConsentWrittenRequired, notWritten?.Code);
        Assert.Equal(ErrorCodes.ConsentSuperseded, ended?.Code);
        Assert.All(
            refusals.Append(organizationWide).Append(notWritten).Append(ended),
            refusal => Assert.False(refusal?.Details.ContainsKey("correlation")));

        foreach (SubjectId account in new[] { none.Account, ordinary.Account, superseded.Account, wide })
        {
            Assert.Equal(0, await PersonsDenialsAsync(account));
        }
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
        IServiceProvider? deployment = null) =>
        (await RefusedWithAsync(granted, permission, deployment))?.Code;

    // The refusal of the check whole, with what it carries.
    private async Task<Error?> RefusedWithAsync(
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

        return outcome.Match(() => (Error?)null, error => error);
    }

    // The check that names no record: whether the account may do this at all within
    // the organization.
    private async Task<ErrorCode?> RefusalAsync(
        SubjectId account,
        Permission permission,
        OrganizationId organization) =>
        (await RefusedWithAsync(account, permission, organization))?.Code;

    private async Task<Error?> RefusedWithAsync(
        SubjectId account,
        Permission permission,
        OrganizationId organization)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(account),
                permission,
                organization,
                TestContext.Current.CancellationToken);

        return outcome.Match(() => (Error?)null, error => error);
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
    private async Task<bool> ListedAsync(Granted granted, IServiceProvider deployment, AccessContext? asking = null)
    {
        await using AsyncServiceScope scope = deployment.CreateAsyncScope();
        await using HostContext reading = host.Context();

        Result<Expression<Func<HostDocument, bool>>> filter = await scope.ServiceProvider
            .GetRequiredService<IAccessGate>()
            .FilterAsync(
                asking ?? AccessContext.Of(granted.Account),
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

    // Both renderings of the one list, the expression first, asked by the account holding
    // the grant or by the context given.
    private async Task<(bool Filter, bool Fragment)> ListedBothWaysAsync(
        Granted granted,
        IServiceProvider deployment,
        AccessContext? asking = null) =>
        (await ListedAsync(granted, deployment, asking), await ListedByFragmentAsync(granted, deployment, asking));

    // A document of a fresh organization whose data subject is a customer holding the
    // written consent the recommendations purpose asks, or none, and the context of the
    // job the host declares, acting for that organization.
    private async Task<Jobbed> JobbedAsync(bool consented)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([HostPermissions.Read], cancellationToken);

        SubjectId customer = await deployment.AccountAsync(cancellationToken);
        ResourceReference workspace = Reference(Workspace);
        ResourceReference record = Reference(Document);

        await deployment.RegisterAsync(workspace, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, workspace, cancellationToken, customer);

        if (consented)
        {
            await RecordAsync(customer, Held(ConsentKind.Written));
        }

        var job = AccessContext.Of(SystemPrincipal.ForOrganization(
            HostFixture.Digest,
            "the nightly digest",
            deployment.Organization));

        return new Jobbed(job, new Granted(customer, record, deployment.Organization));
    }

    // The job's check of the consent-bound action on the record, with the rows the type's
    // derivation is evaluated over.
    private async Task<Error?> JobRefusalAsync(Jobbed jobbed)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using HostContext reading = host.Context();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                jobbed.Job,
                HostPermissions.Recommend,
                jobbed.Written.Record,
                Sources(reading),
                TestContext.Current.CancellationToken);

        return outcome.Match(() => (Error?)null, error => error);
    }

    // The refusals recorded naming the declared job in the organization, which is what
    // the job's denial alert counts (AUTHZ-CONCEAL-004, OPS-ALERT-001).
    private async Task<int> JobDenialsAsync(OrganizationId organization)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.denied' AND principal = @job AND organization = @organization
            """,
            new { job = HostFixture.Digest, organization = organization.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The refusals recorded naming the account as acting, which is what its denial alert
    // counts (AUTHZ-CONCEAL-004, OPS-ALERT-001).
    private async Task<int> PersonsDenialsAsync(SubjectId account)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM identity.audit_records
            WHERE action = 'authz.access.denied' AND acting_subject = @account
            """,
            new { account = account.Value },
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // The same through the fragment, composed into a hand-written query.
    private async Task<bool> ListedByFragmentAsync(Granted granted, IServiceProvider deployment, AccessContext? asking = null)
    {
        Result<SqlFilter> rendered;

        await using (AsyncServiceScope scope = deployment.CreateAsyncScope())
        {
            rendered = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .FragmentAsync(
                    asking ?? AccessContext.Of(granted.Account),
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

    // One job's case: the declared job's context, and the record whose data subject is
    // the account it names.
    private sealed record Jobbed(AccessContext Job, Granted Written);
}
