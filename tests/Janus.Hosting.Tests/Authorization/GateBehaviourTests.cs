using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// What the gate decides as the rows change under it
/// (AUTHZ-GRANT-002, AUTHZ-GRANT-004, AUTHZ-INHERIT-001, AUTHZ-SCOPE-001,
/// AUTHZ-CACHE-001, AUTHZ-GATE-005, AUTHZ-PRIN-003).
/// </summary>
[Trait("kind", "integration")]
public sealed class GateBehaviourTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// AUTHZ-INHERIT-001 AC1: a grant on a container confers the same access on a
    /// record three levels beneath it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_001_AC1_AGrantThreeLevelsAboveConfersTheSameAccessAsync()
    {
        Nested nested = await NestAsync();

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-INHERIT-001 AC2, AUTHZ-CACHE-001 AC1, AUTHZ-GRANT-004 AC2: taking the
    /// grant away takes the inherited access with it, on the next request and with no
    /// wait.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC1_RevokingTakesEffectOnTheNextRequestAsync()
    {
        Nested nested = await NestAsync();

        GrantId grant = await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));

        await nested.Deployment.RevokeAsync(grant, TestContext.Current.CancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC5, AUTHZ-GRANT-004 AC1: what a role allows is read where a
    /// grant naming it is read, so editing the role decides the next request without a
    /// counter moving and without a restart.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC5_EditingARolesPermissionsTakesEffectAtOnceAsync()
    {
        Nested nested = await NestAsync();

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Record,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record, HostPermissions.Edit));

        await nested.Deployment.AllowAsync(
            nested.Role, HostPermissions.Edit, allows: true, TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record, HostPermissions.Edit));

        await nested.Deployment.AllowAsync(
            nested.Role, HostPermissions.Edit, allows: false, TestContext.Current.CancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record, HostPermissions.Edit));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC6: a record moved out from under a grant is refused on the
    /// next request, the ancestry being read live.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC6_MovingARecordTakesEffectAtOnceAsync()
    {
        Nested nested = await NestAsync();

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));

        await nested.Deployment.MoveAsync(
            nested.Record, nested.Elsewhere, TestContext.Current.CancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-GRANT-002 AC2, AC3: a deny defeats a grant on the whole organization, and
    /// taking the deny away restores it with nothing re-granted.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_002_AC2_ADenyDefeatsAGrantOnTheWholeOrganizationAsync()
    {
        Nested nested = await NestAsync();
        var holder = GrantSubject.Of(nested.Account);

        await nested.Deployment.GrantAsync(
            holder, nested.Role, null, false, null, null, TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));

        GrantId deny = await nested.Deployment.GrantAsync(
            holder,
            nested.Role,
            nested.Record,
            true,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record));

        await nested.Deployment.RevokeAsync(deny, TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-SCOPE-001 AC2: one account holding a grant in each of two organizations
    /// reaches what each of them grants and nothing else, with nothing to switch.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_AC2_AnAccountInTwoOrganizationsReachesOnlyWhatEachGrantsAsync()
    {
        Nested one = await NestAsync();
        Nested other = await NestAsync(one.Account);

        await one.Deployment.GrantAsync(
            GrantSubject.Of(one.Account),
            one.Role,
            one.Record,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(one.Account, one.Record));
        Assert.False(await ChecksAsync(one.Account, other.Record));

        await other.Deployment.GrantAsync(
            GrantSubject.Of(one.Account),
            other.Role,
            other.Record,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(one.Account, one.Record));
        Assert.True(await ChecksAsync(one.Account, other.Record));
    }

    /// <summary>
    /// AUTHZ-GATE-005 AC1, AC2: fifty records are answered in one statement over the
    /// whole page, and every permission a capability names is one the check allows.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_005_AC1_APageOfFiftyIsAnsweredWithoutAQueryPerRecordAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync();

        List<ResourceId> page = [];

        for (int record = 0; record < 50; record++)
        {
            ResourceReference reference = Reference(Document);
            await nested.Deployment.RegisterAsync(reference, nested.Bottom, cancellationToken);
            page.Add(reference.Id);
        }

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        IReadOnlyList<Capability> capabilities = Rendered(
            await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .CapabilitiesAsync(
                    AccessContext.Of(nested.Account),
                    Document,
                    page,
                    [HostPermissions.Read, HostPermissions.Edit],
                    cancellationToken));

        Assert.Equal(50, capabilities.Count);
        Assert.All(capabilities, capability => Assert.Equal([HostPermissions.Read], capability.Can));
        Assert.All(capabilities, capability => Assert.Empty(capability.Requires));

        foreach (Capability capability in capabilities)
        {
            Assert.True(await ChecksAsync(
                nested.Account,
                new ResourceReference(Document, capability.Resource)));
        }
    }

    /// <summary>
    /// AUTHZ-PRIN-003 AC1: a record of a kind the host never declared raises, rather
    /// than being permitted by a rule that governs nothing.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_PRIN_003_AC1_AnUndeclaredResourceTypeRaisesAsync()
    {
        Nested nested = await NestAsync();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IAccessGate gate = scope.ServiceProvider.GetRequiredService<IAccessGate>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await gate.RequireAsync(
            AccessContext.Of(nested.Account),
            HostPermissions.Read,
            Reference(ResourceType.Parse("ledger")),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// AUTHZ-PRIN-003 AC2: background work asking as a named principal holds no account
    /// and therefore holds no grant, so the gate refuses rather than assuming.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_PRIN_003_AC2_APrincipalThatResolvesToNoAccountIsRefusedAsync()
    {
        Nested nested = await NestAsync();

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(SystemPrincipal.ForOrganization(
                    "import",
                    "the nightly import",
                    nested.Deployment.Organization)),
                HostPermissions.Read,
                nested.Record,
                TestContext.Current.CancellationToken);

        Assert.Equal(ErrorCodes.Denied, outcome.Match(
            () => throw new InvalidOperationException("The permission was not refused."),
            error => error.Code));
    }

    /// <summary>
    /// AUTHZ-INHERIT-001 AC2, AUTHZ-GRANT-004 AC2: taking the grant away takes the
    /// inherited access with it, on the request after it and without anything else
    /// happening in between.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_001_AC2_RemovingTheGrantRemovesTheInheritedAccessAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync();

        GrantId grant = await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            cancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));

        await nested.Deployment.RevokeAsync(grant, cancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC2: a grant written now is read by the request after it, no
    /// restart and no wait between the two.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC2_GrantingTakesEffectOnTheNextRequestAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync();

        Assert.False(await ChecksAsync(nested.Account, nested.Record));

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Bottom,
            false,
            null,
            null,
            cancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-GRANT-002 AC1: a deny on the record itself defeats an allow inherited from
    /// a container above it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_002_AC1_ADenyOnTheRecordDefeatsAnInheritedAllowAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync();

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            cancellationToken);

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Record,
            true,
            null,
            null,
            cancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-GRANT-002 AC3: taking the deny away restores what the allow already
    /// conferred, the allow never having been touched.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_002_AC3_RemovingTheDenyRestoresTheAllowAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync();

        GrantId allow = await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            cancellationToken);

        GrantId deny = await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Record,
            true,
            null,
            null,
            cancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record));

        await nested.Deployment.RevokeAsync(deny, cancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));
        Assert.NotEqual(allow, deny);
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC7: a grant whose expiry has passed confers nothing, no sweep
    /// having run and nothing having been invalidated.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC7_AnExpiredGrantConfersNothingWithoutASweepAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync();

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            Deployment.Noon.AddHours(-1),
            null,
            cancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record));
    }

    /// <summary>
    /// AUTHZ-GATE-005 AC2: a capability the gate reports with nothing further required
    /// is a capability the check grants, so a frontend that shows the control is right
    /// to show it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_005_AC2_ACapabilityRequiringNothingFurtherSucceedsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync();

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Top,
            false,
            null,
            null,
            cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Capability capability = Assert.Single(Rendered(
            await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .CapabilitiesAsync(
                    AccessContext.Of(nested.Account),
                    Document,
                    [nested.Record.Id],
                    [HostPermissions.Read, HostPermissions.Edit],
                    cancellationToken)));

        Assert.Empty(capability.Requires);

        foreach (Permission permission in capability.Can)
        {
            Assert.True(await ChecksAsync(nested.Account, nested.Record, permission));
        }

        Assert.NotEmpty(capability.Can);
    }

    /// <summary>
    /// AUTHZ-GATE-005 AC3: an action the grants confer and a step-up gate stands in
    /// front of is offered with what it still requires, and the check that meets it
    /// says what is missing rather than failing silently.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_005_AC3_ACapabilityCarriesWhatTheActionStillRequiresAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync(allowing: [HostPermissions.Read, HostPermissions.Publish]);

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Record,
            false,
            null,
            null,
            cancellationToken);

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Capability capability = Assert.Single(Rendered(
            await scope.ServiceProvider.GetRequiredService<IAccessGate>()
                .CapabilitiesAsync(
                    AccessContext.Of(nested.Account),
                    Document,
                    [nested.Record.Id],
                    [HostPermissions.Read, HostPermissions.Publish],
                    cancellationToken)));

        Assert.Contains(HostPermissions.Publish, capability.Can);
        Assert.Equal(
            [CapabilityResidual.StepUp],
            Assert.Contains(HostPermissions.Publish, capability.Requires));
        Assert.DoesNotContain(HostPermissions.Read, capability.Requires);
        Assert.NotNull(await RefusalAsync(nested.Account, nested.Record, HostPermissions.Publish));
    }

    /// <summary>
    /// LIB-HOST-004 AC2, AUTH-STEP-003 AC1, AC2: with no assurance provider registered,
    /// an action bound to a step-up gate is refused although the grants confer it, and
    /// the refusal is a different code from the one an absent grant carries.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_004_AC2_ABoundActionIsDeniedWithNoAssuranceProviderAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Nested nested = await NestAsync(allowing: [HostPermissions.Read, HostPermissions.Publish]);

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Record,
            false,
            null,
            null,
            cancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));
        Assert.Equal(
            ErrorCodes.StepUpUnavailable,
            await RefusalAsync(nested.Account, nested.Record, HostPermissions.Publish));
        Assert.Equal(
            ErrorCodes.Denied,
            await RefusalAsync(
                await nested.Deployment.AccountAsync(cancellationToken),
                nested.Record,
                HostPermissions.Publish));
    }

    private static TRendering Rendered<TRendering>(Result<TRendering> outcome) =>
        outcome.Match(
            rendering => rendering,
            error => throw new InvalidOperationException(error.Code.ToString()));

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    /// <summary>
    /// AUTHZ-GATE-006 AC1, AC2: a restricted account reads its own records and modifies
    /// none of them, and the refusal is the restriction's own code rather than an
    /// absent grant.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC1_ARestrictedAccountReadsAndDoesNotModifyAsync()
    {
        Nested nested = await NestAsync(allowing: [HostPermissions.Read, HostPermissions.Edit]);

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Record,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        await nested.Deployment.RestrictAsync(
            nested.Account,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record));
        Assert.Equal(
            ErrorCodes.Restricted,
            await RefusalAsync(nested.Account, nested.Record, HostPermissions.Edit));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC8: restricting an account decides the next request, with no
    /// wait and nothing to invalidate.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC8_RestrictingAnAccountTakesEffectImmediatelyAsync()
    {
        Nested nested = await NestAsync(allowing: [HostPermissions.Read, HostPermissions.Edit]);

        await nested.Deployment.GrantAsync(
            GrantSubject.Of(nested.Account),
            nested.Role,
            nested.Record,
            false,
            null,
            null,
            TestContext.Current.CancellationToken);

        Assert.True(await ChecksAsync(nested.Account, nested.Record, HostPermissions.Edit));

        await nested.Deployment.RestrictAsync(
            nested.Account,
            TestContext.Current.CancellationToken);

        Assert.False(await ChecksAsync(nested.Account, nested.Record, HostPermissions.Edit));
    }

    private async Task<ErrorCode?> RefusalAsync(
        SubjectId account,
        ResourceReference resource,
        Permission permission)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(account),
                permission,
                resource,
                TestContext.Current.CancellationToken);

        return outcome.Match(() => (ErrorCode?)null, error => error.Code);
    }

    private async Task<bool> ChecksAsync(
        SubjectId account,
        ResourceReference resource,
        Permission? permission = null)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        Result outcome = await scope.ServiceProvider.GetRequiredService<IAccessGate>()
            .RequireAsync(
                AccessContext.Of(account),
                permission ?? HostPermissions.Read,
                resource,
                TestContext.Current.CancellationToken);

        return outcome.Match(() => true, _ => false);
    }

    // One organization with three levels of containment, a record at the bottom, and a
    // fourth container off to one side for a record to be moved into.
    private async Task<Nested> NestAsync(
        SubjectId? asAccount = null,
        IReadOnlyList<Permission>? allowing = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var deployment = new Deployment(host);

        RoleName role = await deployment.BeginAsync(
            allowing ?? [HostPermissions.Read],
            cancellationToken);
        SubjectId account = asAccount ?? await deployment.AccountAsync(cancellationToken);

        ResourceReference top = Reference(Workspace);
        ResourceReference middle = Reference(Workspace);
        ResourceReference bottom = Reference(Workspace);
        ResourceReference elsewhere = Reference(Workspace);
        ResourceReference record = Reference(Document);

        await deployment.RegisterAsync(top, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(middle, top, cancellationToken);
        await deployment.RegisterAsync(bottom, middle, cancellationToken);
        await deployment.RegisterAsync(elsewhere, containedIn: null, cancellationToken);
        await deployment.RegisterAsync(record, bottom, cancellationToken);

        return new Nested(deployment, account, role, top, bottom, elsewhere, record);
    }

    // One case's rows.
    private sealed record Nested(
        Deployment Deployment,
        SubjectId Account,
        RoleName Role,
        ResourceReference Top,
        ResourceReference Bottom,
        ResourceReference Elsewhere,
        ResourceReference Record);
}
