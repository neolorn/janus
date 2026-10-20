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

    private async Task<int> HostRowsAsync(string document)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM host.documents WHERE id = @id;",
            new { id = document });
    }
}
