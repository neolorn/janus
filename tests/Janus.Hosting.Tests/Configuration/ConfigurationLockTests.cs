using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Configuration;
using Janus.Authentication.Factors;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Configuration;

/// <summary>
/// A runtime change decided on the value in force under its row's lock, over the
/// library's own database with two connections (OPS-CFG-002 AC6, X3 of D-166, 178).
/// </summary>
[Trait("kind", "integration")]
public sealed class ConfigurationLockTests(HostFixture host) : IClassFixture<HostFixture>
{
    private static readonly StepUpChallenge Satisfied =
        new(StepUpOutcome.Satisfied, AssuranceLevel.Aal2, PhishingResistant: false, TimeSpan.FromMinutes(5), [], null);

    // How long the case waits for the second change to reach the lock before it fails
    // rather than hangs.
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    /// <summary>
    /// OPS-CFG-002 AC6: a change waits for a concurrent one on the same key and
    /// classifies against the value that one committed. Lengthening the inactivity
    /// timeout from one hour to thirty minutes' worth is a tightening against the value
    /// both read first, and a loosening against the ten minutes the first change put in
    /// force; decided under the lock it is the loosening, refused to a person without
    /// <c>system:administer</c> though its step-up is met, and the first change's value
    /// stays.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task OPS_CFG_002_AC6_AChangeWaitsForAConcurrentOneAndClassifiesAgainstItAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DurationSetting setting = Settings.SessionAal2Inactivity;

        await WriteAsync(
            "INSERT INTO identity.settings (key, value) VALUES (@Key, 'PT1H') "
                + "ON CONFLICT (key) DO UPDATE SET value = excluded.value;",
            setting,
            cancellationToken);

        try
        {
            await using AsyncServiceScope first = host.Services.CreateAsyncScope();
            await using AsyncServiceScope second = host.Services.CreateAsyncScope();

            IUnitOfWork holding = first.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // The first change runs inside a transaction the case keeps open, so its row
            // stays locked and its value uncommitted until the case commits it.
            await holding.BeginAsync(cancellationToken);

            Result tightened = await first.ServiceProvider.GetRequiredService<ConfigurationAdministration>()
                .ChangeAsync(
                    setting,
                    TimeSpan.FromMinutes(10),
                    "a shorter idle window",
                    Satisfied,
                    AccessContext.Of(SubjectId.New(first.ServiceProvider.GetRequiredService<RandomNumberGenerator>())),
                    cancellationToken);

            Task<Result> waiting = second.ServiceProvider.GetRequiredService<ConfigurationAdministration>()
                .ChangeAsync(
                    setting,
                    TimeSpan.FromMinutes(30),
                    "a longer idle window",
                    Satisfied,
                    AccessContext.Of(SubjectId.New(second.ServiceProvider.GetRequiredService<RandomNumberGenerator>())),
                    cancellationToken)
                .AsTask();

            await BlockedAsync(cancellationToken);

            Assert.False(waiting.IsCompleted);

            await holding.CommitAsync(cancellationToken);

            Error refusal = (await waiting.WaitAsync(Bound, cancellationToken)).Match(
                () => throw new Xunit.Sdk.XunitException("The loosening was admitted."),
                error => error);

            await using NpgsqlConnection connection = await host.OpenAsync();

            Assert.True(tightened.Match(() => true, _ => false));
            Assert.Equal(ErrorCodes.Denied, refusal.Code);
            Assert.Equal(
                "PT10M",
                await connection.ExecuteScalarAsync<string>(
                    "SELECT value FROM identity.settings WHERE key = @Key",
                    new { Key = setting.Key.ToString() }));
        }
        finally
        {
            await WriteAsync("DELETE FROM identity.settings WHERE key = @Key;", setting, cancellationToken);
        }
    }

    // The second change is waiting on the row lock the first holds, as the database
    // itself reports it, so the case commits the first only once the second has read
    // nothing yet.
    private async Task BlockedAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(Bound);

        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE '%FOR UPDATE%'",
                   cancellationToken: bounded.Token)) == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), bounded.Token);
        }
    }

    private async Task WriteAsync(string statement, DurationSetting setting, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            statement,
            new { Key = setting.Key.ToString() },
            cancellationToken: cancellationToken));
    }
}
