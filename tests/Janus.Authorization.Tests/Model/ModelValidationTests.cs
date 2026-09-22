using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Model;
using Janus.Authorization.Roles;
using Janus.Authorization.Tests.Roles;
using Janus.Core;
using Xunit;

namespace Janus.Authorization.Tests.Model;

/// <summary>
/// The two checks of the model the declaration alone cannot decide
/// (AUTHZ-MODEL-004, AUTHZ-DERIVE-004).
/// </summary>
[Trait("kind", "unit")]
public sealed class ModelValidationTests
{
    /// <summary>
    /// AUTHZ-MODEL-004 AC1: a role is written at runtime, so what it allows is read at
    /// startup, and one allowing a permission the model does not declare stops the
    /// deployment with its own code.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_MODEL_004_AC1_ARoleAllowingAnUndeclaredPermissionIsRefusedAsync()
    {
        var roles = new RolesInMemory();

        await roles.CreateAsync(
            Role.Of(RoleName.Parse("archivist"), [Permission.Parse("article:archive")]),
            TestContext.Current.CancellationToken);

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await Validation(roles, Indexed()).ValidateAsync(
                TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.StartupUndeclaredPermission, refused.Failure?.Code);
        Assert.NotEmpty(refused.Failure!.Details);
    }

    /// <summary>
    /// AUTHZ-DERIVE-004 AC1, AUTHZ-MODEL-004 AC1: a derived grant is only as fast as
    /// the join it performs into the host's relation, so a derivation naming a column
    /// no index reaches stops the deployment with its own code.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_DERIVE_004_AC1_ADerivationNamingAnUnindexedColumnIsRefusedAsync()
    {
        var indexes = new IndexesInMemory();
        indexes.Index("host.folders", "reviewer");

        StartupException refused = await Assert.ThrowsAsync<StartupException>(
            async () => await Validation(new RolesInMemory(), indexes).ValidateAsync(
                TestContext.Current.CancellationToken));

        Assert.Equal(ErrorCodes.StartupUnindexedDerivation, refused.Failure?.Code);
        Assert.NotEmpty(refused.Failure!.Details);
    }

    /// <summary>
    /// AUTHZ-MODEL-004: a deployment whose roles allow what the model declares and
    /// whose derivation columns are indexed passes both checks, so the validation
    /// stops the deployments it names and no others.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task ValidateAsync_RolesDeclaredAndColumnsIndexed_PassesAsync()
    {
        var roles = new RolesInMemory();

        await roles.CreateAsync(
            Role.Of(RoleName.Parse("reader"), [Permission.Parse("article:read")]),
            TestContext.Current.CancellationToken);

        await Validation(roles, Indexed()).ValidateAsync(TestContext.Current.CancellationToken);
    }

    // The relation the host's domain declares its relationship over, reachable by both
    // columns the relationship names.
    private static IndexesInMemory Indexed()
    {
        var indexes = new IndexesInMemory();

        indexes.Index("host.folders", "reviewer");
        indexes.Index("host.folders", "id");

        return indexes;
    }

    private static ModelValidation Validation(IRoleStore roles, IIndexCatalogue indexes) =>
        new(AuthorizationModel.Of(HostDomain.Declared().Build()), roles, indexes);
}
