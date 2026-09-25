using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Identity.Audit;
using Janus.Storage.Authorization.Roles;
using Janus.Storage.Identity.Audit;
using Xunit;

namespace Janus.Storage.Tests.Authorization;

/// <summary>
/// What a change to a role leaves in the trail (AUTHZ-GRANT-004, OPS-CFG-007,
/// IDN-AUD-001).
/// </summary>
/// <remarks>
/// The port implementation is tested against the real database (D-156). One database
/// serves the class, so each test writes with an actor of its own.
/// </remarks>
[Trait("kind", "integration")]
public sealed class RoleAuditTests(DatabaseFixture database)
    : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly RoleName Editor = RoleName.Parse("editor");

    private readonly Deployment _deployment = new(database);

    /// <inheritdoc/>
    public void Dispose() => _deployment.Dispose();

    /// <summary>
    /// AUTHZ-GRANT-004 and IDN-AUD-001 AC1: a definition is recorded with the role, what
    /// it permitted, what it permits, the reason, and the actor as both identities.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_ADefinitionRecordsWhatTheRoleWasAndBecameAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId actor = await _deployment.AccountAsync(now);

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).DefinedAsync(
                Editor,
                [Permission.Parse("document:read")],
                [Permission.Parse("document:edit"), Permission.Parse("document:read")],
                "Editors now edit.",
                actor,
                now,
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(actor));

        Assert.Equal(AuditActions.RoleDefined, read.Action);
        Assert.Equal(actor, read.ActingSubject);
        Assert.Equal(actor, read.EffectiveSubject);
        Assert.Null(read.Organization);
        Assert.Equal("editor", read.Details["role"].GetString());
        Assert.Equal("document:read", read.Details["before"][0].GetString());
        Assert.Equal(2, read.Details["after"].GetArrayLength());
        Assert.Equal("Editors now edit.", read.Details["reason"].GetString());
    }

    /// <summary>
    /// AUTHZ-GRANT-004: a removal is recorded with what the role permitted and nothing
    /// after it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_GRANT_004_ARemovalRecordsWhatTheRolePermittedAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SubjectId actor = await _deployment.AccountAsync(now);

        await using (StoreContext writing = database.Context())
        {
            await Audit(writing).RemovedAsync(
                Editor,
                [Permission.Parse("document:read")],
                "No longer used.",
                actor,
                now,
                TestContext.Current.CancellationToken);

            await writing.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        AuditRecord read = Assert.Single(await RecordsAsync(actor));

        Assert.Equal(AuditActions.RoleRemoved, read.Action);
        Assert.Equal(1, read.Details["before"].GetArrayLength());
        Assert.Equal(JsonValueKind.Null, read.Details["after"].ValueKind);
    }

    private RoleAudit Audit(StoreContext context) =>
        new(new AuditStore(context, _deployment.Keys, _deployment.Randomness), TimeProvider.System);

    private async Task<IReadOnlyList<AuditRecord>> RecordsAsync(SubjectId actor)
    {
        await using StoreContext reading = database.Context();

        return await new AuditStore(reading, _deployment.Keys, _deployment.Randomness)
            .FindBySubjectAsync(actor, TestContext.Current.CancellationToken);
    }
}
