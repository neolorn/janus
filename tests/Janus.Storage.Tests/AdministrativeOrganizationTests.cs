using System;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Organizations;
using Janus.Storage.Authentication.Policies;
using Janus.Storage.Identity.Organizations;
using Janus.Storage.Privacy.Policies;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// Which organization administers the deployment, as each area reads it: the one
/// bootstrap marks, and none before it has (IDN-ORG-001, AUTHZ-SCOPE-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class AdministrativeOrganizationTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// IDN-ORG-001: an organization without the mark is never read as the
    /// administrative one, and the marked one is read by both areas.
    /// </summary>
    [Fact]
    public async Task IDN_ORG_001_TheMarkedOrganizationIsTheAdministrativeOneAsync()
    {
        await CreateAsync(Organization.Create(
            new OrganizationId(Guid.CreateVersion7()),
            "Branch " + Guid.NewGuid().ToString("N"),
            Noon));

        await using (StoreContext before = database.Context())
        {
            Assert.Null(await new AdministrativeOrganization(before)
                .FindAsync(TestContext.Current.CancellationToken));
            Assert.Null(await new PrivacyAdministrativeOrganization(before)
                .FindAsync(TestContext.Current.CancellationToken));
        }

        var administration = new OrganizationId(Guid.CreateVersion7());

        await CreateAsync(Organization.CreateAdministrative(
            administration,
            "Administration " + Guid.NewGuid().ToString("N"),
            Noon));

        await using StoreContext after = database.Context();

        Assert.Equal(
            administration,
            await new AdministrativeOrganization(after).FindAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            administration,
            await new PrivacyAdministrativeOrganization(after).FindAsync(TestContext.Current.CancellationToken));
    }

    private async Task CreateAsync(Organization organization)
    {
        await using StoreContext writing = database.Context();

        await new OrganizationStore(writing).CreateAsync(organization, TestContext.Current.CancellationToken);
        await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
