using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using Janus.Core.Configuration;
using Janus.Storage.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// What the one connection accessor hands out, and what a hand-written query sees
/// through it (OPS-DATA-002).
/// </summary>
/// <remarks>
/// The record is the settings row, which <c>Janus.Storage</c> owns, so the write goes
/// through the context and the read through the accessor without any project seeing
/// another's internals (D-154).
/// </remarks>
[Trait("kind", "integration")]
public sealed class DataConnectionsTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string Read = "SELECT value FROM identity.settings WHERE key = @key";

    private const string Write = "INSERT INTO identity.settings (key, value) VALUES (@key, @value)";

    /// <summary>
    /// OPS-DATA-002 AC1: a hand-written query inside the operation's transaction sees
    /// what a hand-written statement wrote in it while the write is still uncommitted,
    /// which a second connection outside the transaction does not see, and the write
    /// is gone once the transaction rolls back.
    /// </summary>
    [Fact]
    public async Task OPS_DATA_002_AC1_AHandWrittenQuerySeesTheTransactionsOwnWritesAsync()
    {
        const string key = "takedown.grace";
        await using StoreContext context = database.Context();
        await using NpgsqlConnection separate = await database.OpenAsync();

        await using (var work = new UnitOfWork(context))
        {
            await work.BeginAsync(TestContext.Current.CancellationToken);

            AmbientConnection ambient = await new DataConnections(context)
                .UseAsync(TestContext.Current.CancellationToken);

            await ambient.Connection.ExecuteAsync(Write, new { key, value = "P20D" }, ambient.Transaction);

            Assert.Equal(
                "P20D",
                await ambient.Connection.QuerySingleOrDefaultAsync<string>(Read, new { key }, ambient.Transaction));
            Assert.Null(await separate.QuerySingleOrDefaultAsync<string>(Read, new { key }));
        }

        Assert.Null(await separate.QuerySingleOrDefaultAsync<string>(Read, new { key }));
    }

    /// <summary>
    /// OPS-DATA-002 AC3: visibility across the two tools inside one transaction, on the
    /// settings row <c>Janus.Storage</c> owns: written through the context, it is read
    /// back through the accessor before anything commits (D-154).
    /// </summary>
    [Fact]
    public async Task OPS_DATA_002_AC3_AContextWriteIsReadThroughTheAccessorInOneTransactionAsync()
    {
        var key = ConfigurationKey.Parse("account.deletion.grace");
        await using StoreContext context = database.Context();
        await using var work = new UnitOfWork(context);
        await work.BeginAsync(TestContext.Current.CancellationToken);

        context.Settings.Add(new SettingRecord { Key = key, Value = "P45D" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AmbientConnection ambient = await new DataConnections(context)
            .UseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            "P45D",
            await ambient.Connection.QuerySingleOrDefaultAsync<string>(
                Read,
                new { key = key.ToString() },
                ambient.Transaction));
    }

    /// <summary>
    /// OPS-DATA-002 AC1: the accessor hands out the operation's transaction, which is
    /// what makes the visibility above hold rather than the query happening to run on
    /// the same connection.
    /// </summary>
    [Fact]
    public async Task OPS_DATA_002_AC1_TheAccessorCarriesTheOperationsTransactionAsync()
    {
        await using StoreContext context = database.Context();
        var connections = new DataConnections(context);

        AmbientConnection outside = await connections.UseAsync(TestContext.Current.CancellationToken);

        Assert.Null(outside.Transaction);

        await using var work = new UnitOfWork(context);
        await work.BeginAsync(TestContext.Current.CancellationToken);

        AmbientConnection inside = await connections.UseAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(inside.Transaction);
        Assert.Same(outside.Connection, inside.Connection);
    }

    /// <summary>
    /// OPS-DATA-002 AC1: the connection comes back open, so nothing downstream opens
    /// one of its own to make a query work.
    /// </summary>
    [Fact]
    public async Task OPS_DATA_002_AC1_TheConnectionComesBackOpenAsync()
    {
        await using StoreContext context = database.Context();

        AmbientConnection ambient = await new DataConnections(context)
            .UseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(System.Data.ConnectionState.Open, ambient.Connection.State);
        Assert.IsAssignableFrom<DbConnection>(ambient.Connection);
    }
}
