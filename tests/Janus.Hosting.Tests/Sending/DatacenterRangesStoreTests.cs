using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Sending;

/// <summary>
/// The datacenter range signal in a deployment that declared no range source, over the
/// store: the signal does not fire, the registration goes on, and the absence is on
/// record as a degradation for as long as the signal is among those the deployment
/// counts (AUTH-ABUSE-008, LIB-HOST-001).
/// </summary>
/// <param name="host">The deployment the test runs against.</param>
[Trait("kind", "integration")]
public sealed class DatacenterRangesStoreTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Whole = "2001:db8:8:8::1";
    private const string Source = "2001:db8:8:8::/64";

    /// <summary>
    /// LIB-HOST-001 AC7, AUTH-ABUSE-008 AC3: with no range source declared a
    /// registration begins, no signal is recorded, and <c>degradation</c> is raised
    /// under <c>botdefence.ranges.absent</c>, committed whatever the request then does;
    /// once the deployment takes <c>datacenterRange</c> out of
    /// <c>abuse.botdefence.signals</c> nothing more is raised.
    /// </summary>
    /// <returns>The work of running it.</returns>
    [Fact]
    public async Task LIB_HOST_001_AC7_WithNoRangeSourceTheAbsenceIsRaisedWhileTheSignalIsCountedAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await BeginAsync(cancellationToken);

        Assert.Equal(1, await RaisedAsync());
        Assert.Equal(0, await SignalledAsync());

        await using (NpgsqlConnection connection = await host.OpenAsync())
        {
            _ = await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO identity.settings (key, value) VALUES (@key, @value)",
                new
                {
                    key = Settings.AbuseBotDefenceSignals.Key.ToString(),
                    value = Settings.AbuseBotDefenceSignals.Write(
                        new HashSet<BotDefenceSignal> { BotDefenceSignal.RepeatedAttempts }),
                },
                cancellationToken: cancellationToken));
        }

        await BeginAsync(cancellationToken);

        Assert.Equal(1, await RaisedAsync());
    }

    private async Task BeginAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();

        (await scope.ServiceProvider.GetRequiredService<IRegistration>()
                .BeginAsync(
                    signedIn: null,
                    "web",
                    "en",
                    Whole,
                    Source,
                    invitationToken: null,
                    challengeToken: null,
                    cancellationToken))
            .Switch(
                _ => { },
                error => throw new Xunit.Sdk.XunitException($"The begin was refused: {error.Code}."));
    }

    private async Task<int> RaisedAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*)::int FROM identity.raised_alerts
            WHERE condition = 'degradation' AND idempotency_key LIKE @key
            """,
            new { key = Alerts.Key(AlertCondition.Degradation, "botdefence.ranges.absent", named: null) + "@%" });
    }

    private async Task<int> SignalledAsync()
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM identity.audit_records WHERE action = @Action",
            new { Action = AuditActions.BotDefenceSignalled.ToString() });
    }
}
