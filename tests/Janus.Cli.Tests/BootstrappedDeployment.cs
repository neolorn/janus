using System;
using System.Threading.Tasks;
using Janus.Storage.Tests;
using Npgsql;
using Xunit;

namespace Janus.Cli.Tests;

/// <summary>
/// A fresh database the migrations have raised, and the one bootstrap run against it
/// that every case reads the outcome of.
/// </summary>
/// <remarks>
/// A deployment is bootstrapped once, so the run happens here and the cases only look
/// at what it left, or run it again to be refused.
/// </remarks>
public sealed class BootstrappedDeployment : IAsyncLifetime
{
    private readonly DatabaseFixture _database = new();

    /// <summary>
    /// The connection bootstrap runs under.
    /// </summary>
    public string ConnectionString => _database.ConnectionString;

    /// <summary>
    /// What the one bootstrap returned.
    /// </summary>
    public Invocation First { get; private set; } = new(-1, string.Empty, string.Empty);

    /// <summary>
    /// Opens a connection to the database, for a case to read what bootstrap wrote.
    /// </summary>
    /// <returns>The open connection.</returns>
    public ValueTask<NpgsqlConnection> OpenAsync() => _database.OpenAsync();

    /// <summary>
    /// Creates another database on the instance with no migration applied, for a case
    /// that makes a database ready from nothing.
    /// </summary>
    /// <param name="name">The database's name.</param>
    /// <returns>How to reach it.</returns>
    public ValueTask<string> CreateDatabaseAsync(string name) => _database.CreateDatabaseAsync(name);

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();

        First = await Invocation.PipedAsync(Invocation.Bootstrap(), Invocation.Keys(ConnectionString));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
