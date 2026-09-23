using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Janus.Authorization.Roles;
using Janus.Core;
using Janus.Privacy.Records;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Privacy.Records;
using Xunit;

namespace Janus.Storage.Tests.Privacy;

/// <summary>
/// The two ports the records of processing read what a derivation cannot give them
/// through: the three supplied fields, and the roles with their permissions
/// (PRIV-ROPA-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class ComplianceStoreTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Assessments = ["wiki/lia-2026", "wiki/dpia-2026"];

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// PRIV-ROPA-001 AC2: a deployment that has stated nothing reads back nothing,
    /// which is what the register flags rather than an empty row it invented; the
    /// three fields are then read back as they were stated, and a second statement
    /// replaces the first rather than standing beside it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_AC2_TheSuppliedFieldsAreReadBackAndASecondStatementReplacesThemAsync()
    {
        ComplianceRecord nothing = await HeldAsync();

        Assert.Null(nothing.DataOwner);
        Assert.Null(nothing.OrganisationalSecurityMeasures);
        Assert.Empty(nothing.AssessmentLinks);

        await RecordedAsync(new ComplianceRecord(
            "the head of customer operations",
            "annual training and a clear-desk rule",
            Assessments));

        ComplianceRecord first = await HeldAsync();

        Assert.Equal("the head of customer operations", first.DataOwner);
        Assert.Equal("annual training and a clear-desk rule", first.OrganisationalSecurityMeasures);
        Assert.Equal(Assessments, first.AssessmentLinks);

        await RecordedAsync(new ComplianceRecord("the data protection officer", null, []));

        ComplianceRecord second = await HeldAsync();

        Assert.Equal("the data protection officer", second.DataOwner);
        Assert.Null(second.OrganisationalSecurityMeasures);
        Assert.Empty(second.AssessmentLinks);
    }

    /// <summary>
    /// PRIV-ROPA-001: the roles the register reports access from are the deployment's
    /// own, each with what it allows and nothing of who holds it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_ROPA_001_TheRolesComeBackWithWhatEachAllowsAsync()
    {
        RoleName name = await CreatedAsync([Permissions.AuditRead, Permissions.GrantManage]);

        await using StoreContext reading = database.Context();

        IReadOnlyDictionary<string, IReadOnlyList<Permission>> allowed =
            await new RegisterRoles(new RoleStore(reading))
                .AllowedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [Permissions.AuditRead, Permissions.GrantManage],
            allowed[name.ToString()]);
    }

    private async Task<RoleName> CreatedAsync(IReadOnlyList<Permission> permissions)
    {
        var name = RoleName.Parse("role" + Guid.NewGuid().ToString("n")[..8]);

        await using StoreContext writing = database.Context();
        await using var transaction = new UnitOfWork(writing);
        await transaction.BeginAsync(TestContext.Current.CancellationToken);

        await new RoleStore(writing).CreateAsync(
            Role.Of(name, permissions),
            TestContext.Current.CancellationToken);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return name;
    }

    private async Task RecordedAsync(ComplianceRecord record)
    {
        await using StoreContext writing = database.Context();

        await new ComplianceStore(writing, new FixedTime(Noon))
            .RecordAsync(record, TestContext.Current.CancellationToken);

        _ = await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ComplianceRecord> HeldAsync()
    {
        await using StoreContext reading = database.Context();

        return await new ComplianceStore(reading, new FixedTime(Noon))
            .ReadAsync(TestContext.Current.CancellationToken);
    }
}
