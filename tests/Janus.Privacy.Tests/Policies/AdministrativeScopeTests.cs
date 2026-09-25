using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Policies;
using Xunit;

namespace Janus.Privacy.Tests.Policies;

/// <summary>
/// Where an operation of the deployment asks for its permission: in the administrative
/// organization and nowhere else (AUTHZ-SCOPE-001, IDN-ORG-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class AdministrativeScopeTests
{
    private static readonly SubjectId Mona =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly OrganizationId Administration =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly OrganizationId Branch =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new() { Organization = Administration };

    private AdministrativeScope Scope => new(_gate, _administrative);

    /// <summary>
    /// AUTHZ-SCOPE-001: the permission held in the administrative organization is
    /// honoured.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_APermissionHeldInTheAdministrativeOrganizationIsHonouredAsync()
    {
        _gate.Grant(Mona, Administration, Permissions.PrivacyRequestManage);

        Assert.Null(await RefusedAsync());
    }

    /// <summary>
    /// AUTHZ-SCOPE-001: the same permission held in another organization the caller
    /// belongs to administers nothing of the deployment.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_SCOPE_001_APermissionHeldInAnotherOrganizationIsRefusedAsync()
    {
        _gate.Grant(Mona, Branch, Permissions.PrivacyRequestManage);

        Assert.Equal(ErrorCodes.Denied, (await RefusedAsync())?.Code);
    }

    /// <summary>
    /// IDN-ORG-001: before bootstrap has marked an organization administrative,
    /// nothing is.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ORG_001_WithoutAnAdministrativeOrganizationEveryOperationIsRefusedAsync()
    {
        _gate.Grant(Mona, Administration, Permissions.PrivacyRequestManage);
        _administrative.Organization = null;

        Assert.Equal(ErrorCodes.Denied, (await RefusedAsync())?.Code);
    }

    private async Task<Error?> RefusedAsync() =>
        await Scope.RefusedAsync(
            AccessContext.Of(Mona),
            Permissions.PrivacyRequestManage,
            TestContext.Current.CancellationToken);
}
