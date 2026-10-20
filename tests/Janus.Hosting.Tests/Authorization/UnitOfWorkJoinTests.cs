using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Janus.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's own context joined to the library's unit of work, so that the host's rows
/// and the library's registrations commit together and roll back together, and the
/// context given back its own connection once the unit of work ends
/// (AUTHZ-INHERIT-002, OPS-DATA-002).
/// </summary>
[Trait("kind", "integration")]
public sealed class UnitOfWorkJoinTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly ResourceType Document = ResourceType.Parse("document");
    private static readonly ResourceType Workspace = ResourceType.Parse("workspace");

    /// <summary>
    /// AUTHZ-INHERIT-002 AC1: a host's own row written through its joined context, and
    /// the registrations and ancestry written beside it, are all gone once the unit of
    /// work rolls back.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC1_AHostsRowWrittenThroughAJoinedContextIsRolledBackWithTheAncestryAsync()
    {
        Case written = await BeginAsync();

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            HostContext context = scope.ServiceProvider.GetRequiredService<HostContext>();

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken);
            await WriteAsync(scope, context, written);
            await work.RollbackAsync();
        }

        Assert.Equal(0, await HostRowsAsync(written.Document));
        Assert.Equal(0, await RegisteredAsync(written.Document) + await RegisteredAsync(written.Workspace));
        Assert.Equal(0, await AncestryOfAsync(written.Document) + await AncestryOfAsync(written.Workspace));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC1: a scope that ends its unit of work without a commit leaves
    /// neither the host's row nor the ancestry, the host's context having been disposed
    /// before the unit of work ended.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC1_AScopeEndedWithoutACommitLeavesNeitherTheHostsRowNorTheAncestryAsync()
    {
        Case written = await BeginAsync();

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            HostContext context = scope.ServiceProvider.GetRequiredService<HostContext>();

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken);
            await WriteAsync(scope, context, written);
        }

        Assert.Equal(0, await HostRowsAsync(written.Document));
        Assert.Equal(0, await AncestryOfAsync(written.Document) + await AncestryOfAsync(written.Workspace));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC1: the same writes committed leave the host's row, the
    /// registrations and the ancestry together, so what the rollback undid was written in
    /// the first place, in the one transaction.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC1_AHostsRowAndTheAncestryCommitTogetherAsync()
    {
        Case written = await BeginAsync();
        int seenElsewhere;

        await using (AsyncServiceScope scope = host.Services.CreateAsyncScope())
        {
            IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            HostContext context = scope.ServiceProvider.GetRequiredService<HostContext>();

            await work.BeginAsync(TestContext.Current.CancellationToken);
            await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken);
            await WriteAsync(scope, context, written);

            seenElsewhere = await HostRowsAsync(written.Document);

            await work.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, seenElsewhere);
        Assert.Equal(1, await HostRowsAsync(written.Document));
        Assert.Equal(2, await RegisteredAsync(written.Document) + await RegisteredAsync(written.Workspace));
        Assert.Equal(2, await AncestryOfAsync(written.Document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC6: a unit of work that has not begun holds nothing to join,
    /// and the call is a fault that leaves the context as it was.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC6_AUnitOfWorkNotBegunIsAFaultAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        HostContext context = scope.ServiceProvider.GetRequiredService<HostContext>();
        DbConnection configured = context.Database.GetDbConnection();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken));

        Assert.Same(configured, context.Database.GetDbConnection());
        Assert.Equal(ConnectionState.Closed, configured.State);
        Assert.Null(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC6: a context whose own connection is open holds a connection of
    /// its own, so joining is a fault, and the context keeps its connection.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC6_AContextWhoseOwnConnectionIsOpenIsAFaultAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using HostContext context = host.Context();

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        DbConnection own = context.Database.GetDbConnection();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken));

        Assert.Same(own, context.Database.GetDbConnection());
        Assert.Equal(ConnectionState.Open, own.State);

        await work.RollbackAsync();
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC6: a context that holds a transaction of its own cannot also
    /// run in the unit of work's, so joining is a fault, and the context keeps it.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC6_AContextThatHoldsATransactionIsAFaultAsync()
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await using HostContext context = host.Context();

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await using IDbContextTransaction own =
            await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken));

        Assert.Same(own, context.Database.CurrentTransaction);

        await work.RollbackAsync();
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC6: a context joined to one unit of work cannot join another
    /// while it is joined: the call is a fault, and what the context then writes still
    /// commits with the unit of work it joined and with nothing of the other.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC6_AnotherUnitOfWorkWhileOneIsJoinedIsAFaultAsync()
    {
        string document = Guid.NewGuid().ToString();

        await using AsyncServiceScope joined = host.Services.CreateAsyncScope();
        await using AsyncServiceScope other = host.Services.CreateAsyncScope();
        IUnitOfWork first = joined.ServiceProvider.GetRequiredService<IUnitOfWork>();
        IUnitOfWork second = other.ServiceProvider.GetRequiredService<IUnitOfWork>();
        HostContext context = joined.ServiceProvider.GetRequiredService<HostContext>();

        await first.BeginAsync(TestContext.Current.CancellationToken);
        await second.BeginAsync(TestContext.Current.CancellationToken);
        await context.Database.UseUnitOfWorkAsync(first, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await context.Database.UseUnitOfWorkAsync(second, TestContext.Current.CancellationToken));

        AmbientConnection ambient = await ((IJoinableUnitOfWork)first).ConnectionAsync(TestContext.Current.CancellationToken);

        Assert.Same(ambient.Connection, context.Database.GetDbConnection());
        Assert.Same(ambient.Transaction, context.Database.CurrentTransaction?.GetDbTransaction());

        context.Documents.Add(new HostDocument { Id = document, Title = "joined" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await second.RollbackAsync();
        await first.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await HostRowsAsync(document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC6: called again for the unit of work it joined, the call
    /// changes nothing: the context stays on the same connection and transaction, a level
    /// inside the unit of work committing gives nothing back, and the outermost commit
    /// commits what it wrote once.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC6_CalledAgainForTheJoinedUnitOfWorkItChangesNothingAsync()
    {
        string document = Guid.NewGuid().ToString();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        HostContext context = scope.ServiceProvider.GetRequiredService<HostContext>();

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken);

        DbConnection connection = context.Database.GetDbConnection();
        IDbContextTransaction? transaction = context.Database.CurrentTransaction;

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken);
        await work.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Same(connection, context.Database.GetDbConnection());
        Assert.Same(transaction, context.Database.CurrentTransaction);

        context.Documents.Add(new HostDocument { Id = document, Title = "joined twice" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, await HostRowsAsync(document));

        await work.CommitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await HostRowsAsync(document));
        Assert.NotSame(connection, context.Database.GetDbConnection());
        Assert.Null(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC6: after the outermost commit a context configured with a
    /// connection string is on a connection of that string again, not the unit of work's,
    /// and what it writes then commits on its own.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC6_AfterTheOutermostCommitTheContextUsesTheConnectionStringItWasConfiguredWithAsync()
    {
        string document = Guid.NewGuid().ToString();

        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        HostContext context = scope.ServiceProvider.GetRequiredService<HostContext>();
        string? configured = context.Database.GetConnectionString();

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken);

        DbConnection ambient = context.Database.GetDbConnection();

        await work.CommitAsync(TestContext.Current.CancellationToken);

        Assert.NotSame(ambient, context.Database.GetDbConnection());
        Assert.Equal(configured, context.Database.GetConnectionString());
        Assert.Null(context.Database.CurrentTransaction);

        context.Documents.Add(new HostDocument { Id = document, Title = "on its own" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await HostRowsAsync(document));
    }

    /// <summary>
    /// AUTHZ-INHERIT-002 AC6: after the outermost rollback a context configured with a
    /// closed connection is on that same connection again, and what it writes then commits
    /// on its own.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task AUTHZ_INHERIT_002_AC6_AfterTheOutermostRollbackTheContextUsesTheConnectionItWasConfiguredWithAsync()
    {
        string document = Guid.NewGuid().ToString();

        await using var own = new NpgsqlConnection(host.ConnectionString);
        await using HostContext context = new(new DbContextOptionsBuilder<HostContext>().UseNpgsql(own).Options);
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        IUnitOfWork work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await work.BeginAsync(TestContext.Current.CancellationToken);
        await context.Database.UseUnitOfWorkAsync(work, TestContext.Current.CancellationToken);

        context.Documents.Add(new HostDocument { Id = Guid.NewGuid().ToString(), Title = "rolled back" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        bool joined = !ReferenceEquals(own, context.Database.GetDbConnection());

        await work.RollbackAsync();
        context.ChangeTracker.Clear();

        Assert.True(joined);
        Assert.Same(own, context.Database.GetDbConnection());
        Assert.Null(context.Database.CurrentTransaction);

        context.Documents.Add(new HostDocument { Id = document, Title = "on its own" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, await HostRowsAsync(document));
    }

    private static ResourceReference Reference(ResourceType type) =>
        new(type, ResourceId.Parse(Guid.NewGuid().ToString()));

    private static void Ok(Result outcome) =>
        outcome.Switch(() => { }, error => Assert.Fail(error.Code.ToString()));

    // The library's registrations and the host's own row, written in one unit of work.
    private static async Task WriteAsync(AsyncServiceScope scope, HostContext context, Case written)
    {
        Ok(await scope.ServiceProvider.GetRequiredService<IResources>().RegisterManyAsync(
            [
                new ResourceRegistration(written.Workspace, written.Organization, null, null),
                new ResourceRegistration(written.Document, written.Organization, written.Workspace, written.Account),
            ],
            TestContext.Current.CancellationToken));

        context.Documents.Add(new HostDocument
        {
            Id = written.Document.Id.ToString(),
            Title = "written with its registration",
            Owner = written.Account,
        });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Case> BeginAsync()
    {
        var deployment = new Deployment(host);

        _ = await deployment.BeginAsync([HostPermissions.Read], TestContext.Current.CancellationToken);
        SubjectId account = await deployment.AccountAsync(TestContext.Current.CancellationToken);

        return new Case(deployment.Organization, account, Reference(Workspace), Reference(Document));
    }

    private async Task<int> HostRowsAsync(ResourceReference document) =>
        await HostRowsAsync(document.Id.ToString());

    private async Task<int> HostRowsAsync(string document)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM host.documents WHERE id = @id;",
            new { id = document });
    }

    private async Task<int> RegisteredAsync(ResourceReference resource)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM identity.resources WHERE resource_type = @type AND resource_id = @id;",
            new { type = resource.Type.ToString(), id = resource.Id.ToString() });
    }

    private async Task<int> AncestryOfAsync(ResourceReference resource)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM identity.ancestry WHERE resource_type = @type AND resource_id = @id;",
            new { type = resource.Type.ToString(), id = resource.Id.ToString() });
    }

    // One case's organization, the account the document's subject is, and the two
    // records registered.
    private sealed record Case(
        OrganizationId Organization,
        SubjectId Account,
        ResourceReference Workspace,
        ResourceReference Document);
}
