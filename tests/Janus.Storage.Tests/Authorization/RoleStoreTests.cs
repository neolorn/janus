using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Janus.Authorization.Grants;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Storage.Authorization.Grants;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// Roles as the tables hold them: what a role allows is data, edited at runtime, and
/// read live wherever a grant naming it is evaluated (AUTHZ-GRANT-004, AUTHZ-CACHE-001,
/// CONV-TEST-003, CONV-DESIGN-004).
/// </summary>
[Trait("kind", "integration")]
public sealed class RoleStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Deployment _deployment = new(database);

    /// <summary>
    /// AUTHZ-GRANT-004 AC1: a role and the permissions it bundles are rows, written and
    /// read back without anything being restarted or deployed.
    /// </summary>
    [Fact]
    public async Task AUTHZ_GRANT_004_AC1_ARoleAndItsPermissionsAreWrittenAtRuntimeAsync()
    {
        RoleName name = await CreateAsync([Permissions.GrantRead, Permissions.AuditRead]);

        Role role = await ReadAsync(name);

        Assert.True(role.Allows(Permissions.GrantRead));
        Assert.True(role.Allows(Permissions.AuditRead));
        Assert.False(role.Allows(Permissions.GrantManage));
    }

    /// <summary>
    /// AUTHZ-CACHE-001 AC5: editing what a role allows takes effect at once, and raises
    /// nobody's counter, because no cache entry carries a role's permissions.
    /// </summary>
    [Fact]
    public async Task AUTHZ_CACHE_001_AC5_EditingARolesPermissionsRaisesNoCounterAsync()
    {
        SubjectId account = await _deployment.AccountAsync(Noon);
        RoleName name = await CreateAsync([Permissions.GrantRead]);

        long before = await VersionAsync(account);

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await new RoleStore(writing).RecordAsync(
                Role.Of(name, [Permissions.AuditRead, Permissions.GrantManage]),
                TestContext.Current.CancellationToken);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Role edited = await ReadAsync(name);

        Assert.False(edited.Allows(Permissions.GrantRead));
        Assert.True(edited.Allows(Permissions.AuditRead));
        Assert.True(edited.Allows(Permissions.GrantManage));
        Assert.Equal(before, await VersionAsync(account));
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC1: every role is readable in one call, which is what the
    /// startup check reads to find a role naming a permission no host declared.
    /// </summary>
    [Fact]
    public async Task AllAsync_SeveralRoles_ReadsEachWithItsOwnPermissionsAsync()
    {
        RoleName one = await CreateAsync([Permissions.GrantRead]);
        RoleName other = await CreateAsync([Permissions.AuditRead, Permissions.GrantManage]);

        await using StoreContext reading = database.Context();

        IReadOnlyList<Role> roles = await new RoleStore(reading)
            .AllAsync(TestContext.Current.CancellationToken);

        Assert.True(roles.Single(role => role.Name == one).Allows(Permissions.GrantRead));
        Assert.False(roles.Single(role => role.Name == one).Allows(Permissions.AuditRead));
        Assert.True(roles.Single(role => role.Name == other).Allows(Permissions.GrantManage));
    }

    /// <summary>
    /// AUTHZ-GRANT-004 AC1: removing a role takes its permission rows with it, so no row
    /// names a role that is gone.
    /// </summary>
    [Fact]
    public async Task RemoveAsync_ARoleWithPermissions_TakesItsPermissionRowsWithItAsync()
    {
        RoleName name = await CreateAsync([Permissions.GrantRead, Permissions.AuditRead]);

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await new RoleStore(writing).RemoveAsync(name, TestContext.Current.CancellationToken);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using StoreContext reading = database.Context();

        Assert.Null(await new RoleStore(reading).FindAsync(name, TestContext.Current.CancellationToken));
        Assert.Empty(reading.RolePermissions.Where(row => row.Role == name));
    }

    /// <summary>
    /// CONV-DESIGN-004 AC3: a role whose name or one of whose permissions was never read
    /// writes no row through the model's conversions, and takes the rows written beside
    /// it in the same unit of work with it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task CONV_DESIGN_004_AC3_ARoleNameOrPermissionNeverReadWritesNoRowAsync()
    {
        (int Roles, int Permissions) before = await CountAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(
            Role.Of(default, [Permissions.GrantRead])));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(
            Role.Of(RoleName.Parse("role" + Guid.NewGuid().ToString("n")[..8]), [Permissions.GrantRead, default])));

        Assert.Equal(before, await CountAsync());
    }

    /// <summary>
    /// AUTHZ-GRANT-005, AUTHZ-GRANT-004 AC6 (D-212): the roles a role hands out are kept
    /// with it and read back with it, changed as its permissions are, and a role another
    /// role hands out is named so that it is not removed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_005_TheRolesARoleHandsOutAreKeptWithItAsync()
    {
        RoleName handed = await CreateAsync([Permissions.GrantRead]);
        var lead = RoleName.Parse("role" + Guid.NewGuid().ToString("n")[..8]);

        await WriteAsync(Role.Of(lead, [Permissions.GrantRead], [handed, lead]));

        Assert.Equal(new HashSet<RoleName> { handed, lead }, (await ReadAsync(lead)).HandsOut);
        Assert.True(await NamedAsync(handed));

        await using (StoreContext writing = database.Context())
        {
            await using var transaction = new UnitOfWork(writing);
            await transaction.BeginAsync(TestContext.Current.CancellationToken);

            await new RoleStore(writing).RecordAsync(
                Role.Of(lead, [Permissions.GrantRead], [lead]),
                TestContext.Current.CancellationToken);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal([lead], (await ReadAsync(lead)).HandsOut);
        Assert.False(await NamedAsync(handed));
        Assert.False(await NamedAsync(lead));
    }

    /// <summary>
    /// IDN-READ-004, OPS-MIG-001 (D-218): a deployment bootstrap stood up before the five
    /// reads existed gives its <c>system-administrator</c> role each of them once
    /// migrated, every other role keeping what it held, so the role holds every
    /// library-owned permission as chapter 10 section 3 says.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task IDN_READ_004_TheSystemAdministratorTakesTheReadsOnceMigratedAsync()
    {
        string seeded = await database.CreateDatabaseAsync("administrator_reads");
        string giving;

        await using (StoreContext migrating = DatabaseFixture.Context(seeded))
        {
            string[] declared = [.. migrating.GetService<IMigrationsAssembly>().Migrations.Keys.Order(StringComparer.Ordinal)];

            giving = declared.Single(migration =>
                migration.EndsWith("_" + nameof(GiveTheSystemAdministratorTheReads), StringComparison.Ordinal));

            await migrating.GetService<IMigrator>().MigrateAsync(
                declared[Array.IndexOf(declared, giving) - 1],
                TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(seeded);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await connection.ExecuteAsync(
            """
            INSERT INTO identity.roles (name) VALUES ('system-administrator'), ('auditor');
            INSERT INTO identity.role_permissions (role, permission)
            VALUES
                ('system-administrator', 'audit:read'),
                ('system-administrator', 'system:administer'),
                ('auditor', 'audit:read');
            """);

        await using (StoreContext migrating = DatabaseFixture.Context(seeded))
        {
            await migrating.GetService<IMigrator>().MigrateAsync(giving, TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            [
                ("auditor", "audit:read"),
                ("system-administrator", "account:read"),
                ("system-administrator", "audit:read"),
                ("system-administrator", "consent:read"),
                ("system-administrator", "contact:read"),
                ("system-administrator", "membership:read"),
                ("system-administrator", "organization:read"),
                ("system-administrator", "system:administer"),
            ],
            await connection.QueryAsync<(string, string)>(
                "SELECT role, permission FROM identity.role_permissions ORDER BY role, permission"));
    }

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    private async Task<RoleName> CreateAsync(IReadOnlyList<Permission> permissions)
    {
        var name = RoleName.Parse("role" + Guid.NewGuid().ToString("n")[..8]);

        await WriteAsync(Role.Of(name, permissions));

        return name;
    }

    private async Task WriteAsync(Role role)
    {
        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await new RoleStore(writing).CreateAsync(role, TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(int Roles, int Permissions)> CountAsync()
    {
        await using StoreContext reading = database.Context();

        return (
            await reading.Roles.CountAsync(TestContext.Current.CancellationToken),
            await reading.RolePermissions.CountAsync(TestContext.Current.CancellationToken));
    }

    private async Task<Role> ReadAsync(RoleName name)
    {
        await using StoreContext reading = database.Context();

        return (await new RoleStore(reading).FindAsync(name, TestContext.Current.CancellationToken))!;
    }

    private async Task<bool> NamedAsync(RoleName role)
    {
        await using StoreContext reading = database.Context();

        return await new RoleReferences(reading).NamedAsync(role, TestContext.Current.CancellationToken);
    }

    private async Task<long> VersionAsync(SubjectId subject)
    {
        await using StoreContext reading = database.Context();

        return await new GrantStore(reading, new DataConnections(reading))
            .VersionAsync(subject, TestContext.Current.CancellationToken);
    }
}
