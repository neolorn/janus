using System;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Storage.Authentication.Registration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Storage.Tests;

/// <summary>
/// The database channel the waiting screen's stream is woken by, and the interval it
/// falls back to (REG-SESS-003, FE-VER-001).
/// </summary>
[Trait("kind", "integration")]
public sealed class RegistrationSignalsTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private static readonly TimeSpan Far = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Near = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan Tick = TimeSpan.FromTicks(1);

    private readonly DatabaseFixture _database;

    private readonly AlertChannelsInMemory _alerts = new();

    private readonly ServiceProvider _services;

    /// <summary>
    /// Takes the database the class shares and stands up what the signal raises through.
    /// </summary>
    /// <param name="database">The database.</param>
    public RegistrationSignalsTests(DatabaseFixture database)
    {
        _database = database;
        _services = new ServiceCollection()
            .AddSingleton<IAlertChannels>(_alerts)
            .BuildServiceProvider();
    }

    private IServiceScopeFactory Scopes => _services.GetRequiredService<IServiceScopeFactory>();

    /// <inheritdoc/>
    public void Dispose() => _services.Dispose();

    /// <summary>
    /// REG-SESS-003: a wait that nothing signals ends on the interval, which is what a
    /// stream reads the state back on where the channel is not heard. The interval is
    /// taken on the library's clock, so it stands until that clock reaches it, however
    /// long the machine takes, and ends when it does.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_003_AWaitNothingSignalsEndsOnTheIntervalAsync()
    {
        var time = new ManualTime();

        await using var signals = new RegistrationSignals(_database.ConnectionString, Scopes, time);

        Task waiting = signals
            .WaitAsync(RegistrationSessionId.New(TimeProvider.System), Near, TestContext.Current.CancellationToken)
            .AsTask();

        await time.PendingAsync().WaitAsync(Far, TestContext.Current.CancellationToken);
        time.Advance(Near - Tick);

        Assert.NotSame(waiting, await Task.WhenAny(waiting, Task.Delay(Near * 2, TestContext.Current.CancellationToken)));

        time.Advance(Tick);

        Assert.Same(waiting, await Task.WhenAny(waiting, Task.Delay(Far, TestContext.Current.CancellationToken)));
    }

    /// <summary>
    /// REG-SESS-003: the announcement is made inside the transaction that changed the
    /// session, so a wait hears it when that transaction commits and never hears the
    /// one that rolled back.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task REG_SESS_003_AWaitHearsTheCommittedAnnouncementAndNoOtherAsync()
    {
        var time = new ManualTime();

        await using var signals = new RegistrationSignals(_database.ConnectionString, Scopes, time);

        var session = RegistrationSessionId.New(TimeProvider.System);

        // The listening connection is opened by the first wait, so one is made and let
        // go before the announcements, and neither of them races it.
        await WaitedOutAsync(time, signals.WaitAsync(session, Near, TestContext.Current.CancellationToken).AsTask());

        await RaiseAsync(session, commit: false);

        Task abandoned = signals.WaitAsync(session, Near, TestContext.Current.CancellationToken).AsTask();

        await time.PendingAsync().WaitAsync(Far, TestContext.Current.CancellationToken);

        Assert.NotSame(abandoned, await Task.WhenAny(abandoned, Task.Delay(Near * 2, TestContext.Current.CancellationToken)));

        await WaitedOutAsync(time, abandoned);

        Task waiting = signals
            .WaitAsync(session, Far, TestContext.Current.CancellationToken)
            .AsTask();

        await RaiseAsync(session, commit: true);

        Task ended = await Task.WhenAny(waiting, Task.Delay(Far, TestContext.Current.CancellationToken));

        Assert.Same(waiting, ended);
    }

    /// <summary>
    /// OPS-OBS-002, REG-SESS-003: a wait that begins while the channel is not listening,
    /// its connection having ended, raises the degradation naming the registration
    /// channel, and the wait still ends on its interval. The first wait of all opens
    /// the channel and raises nothing, and a wait that begins once the channel listens
    /// again raises nothing further.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_OBS_002_ALostChannelIsRaisedAsADegradationAsync()
    {
        var time = new ManualTime();

        await using var signals = new RegistrationSignals(_database.ConnectionString, Scopes, time);

        var session = RegistrationSessionId.New(TimeProvider.System);

        await WaitedOutAsync(time, signals.WaitAsync(session, Near, TestContext.Current.CancellationToken).AsTask());

        Assert.Empty(_alerts.Raised);

        await EndListeningAsync();

        for (int waits = 0; waits < 100 && _alerts.Raised.IsEmpty; waits++)
        {
            await WaitedOutAsync(time, signals.WaitAsync(session, Near, TestContext.Current.CancellationToken).AsTask());
        }

        AlertRaised raised = Assert.Single(_alerts.Raised);

        Task waiting = signals.WaitAsync(session, Far, TestContext.Current.CancellationToken).AsTask();

        await time.PendingAsync().WaitAsync(Far, TestContext.Current.CancellationToken);
        await ListeningAsync();
        await RaiseAsync(session, commit: true);

        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.Null(raised.Scope);
        Assert.Equal("registration-channel", raised.Details["component"].GetString());
        Assert.Same(waiting, await Task.WhenAny(waiting, Task.Delay(Far, TestContext.Current.CancellationToken)));
        Assert.Single(_alerts.Raised);
    }

    // The database ends the connection the channel is listened on, as a restart or a
    // dropped network path would.
    private async Task EndListeningAsync()
    {
        await using NpgsqlConnection connection = await _database.OpenAsync();

        await using NpgsqlCommand end = new(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE query = 'LISTEN identity_registration'",
            connection);

        _ = await end.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // The channel is listened on again once a connection's last statement is the LISTEN.
    private async Task ListeningAsync()
    {
        await using NpgsqlConnection connection = await _database.OpenAsync();

        await using NpgsqlCommand listening = new(
            "SELECT count(*) FROM pg_stat_activity WHERE query = 'LISTEN identity_registration'",
            connection);

        for (int looks = 0; looks < 200; looks++)
        {
            if (await listening.ExecuteScalarAsync(TestContext.Current.CancellationToken) is long count and > 0)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        Assert.Fail("The channel was not listened on again.");
    }

    // A wait the channel does not end, let run on the machine's clock for the interval
    // and then ended on the library's.
    private static async Task WaitedOutAsync(ManualTime time, Task waiting)
    {
        await time.PendingAsync().WaitAsync(Far, TestContext.Current.CancellationToken);
        _ = await Task.WhenAny(waiting, Task.Delay(Near, TestContext.Current.CancellationToken));
        time.Advance(Near);

        Assert.Same(waiting, await Task.WhenAny(waiting, Task.Delay(Far, TestContext.Current.CancellationToken)));
    }

    private async Task RaiseAsync(RegistrationSessionId session, bool commit)
    {
        await using StoreContext context = _database.Context();

        var connections = new DataConnections(context);

        await using IDbContextTransaction transaction = await context.Database
            .BeginTransactionAsync(TestContext.Current.CancellationToken);

        AmbientConnection ambient = await connections
            .UseAsync(TestContext.Current.CancellationToken);

        await RegistrationChannel.RaiseAsync(
            ambient,
            session,
            TestContext.Current.CancellationToken);

        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
    }
}
