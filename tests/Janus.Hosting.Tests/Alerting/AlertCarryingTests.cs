using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Authentication.Alerting;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Hosting.Alerting;
using Janus.Hosting.Background;
using Janus.Hosting.Tests.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Janus.Hosting.Tests.Alerting;

/// <summary>
/// A raised condition over the library's own database: it is a row of the transaction
/// that raised it, so it reaches the destinations only where that transaction commits
/// (OPS-ALERT-001, D-022).
/// </summary>
[Trait("kind", "integration")]
public sealed class AlertCarryingTests(HostFixture host) : IClassFixture<HostFixture>
{
    private const string Operator = "operator@example.test";

    /// <summary>
    /// OPS-ALERT-001 AC3: a condition raised in a transaction that rolls back leaves no
    /// row and no event, and the next pass of the alert channels carries nothing; one
    /// raised in a transaction that commits is carried by the next pass, once, and has
    /// written one <c>AlertRaised</c>.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task OPS_ALERT_001_AC3_OnlyAConditionWhoseTransactionCommitsReachesTheDestinationsAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var mail = (MailTransportInMemory)host.Services.GetRequiredService<IMailTransport>();

        // Whatever the start of the deployment raised is carried first, so the passes
        // below answer for the two conditions of this case alone.
        _ = await CarriedAsync(cancellationToken);

        int before = mail.Taken.Count(taken => taken.Destination.Value == Operator);
        string abandoned = Guid.NewGuid().ToString("N");
        string kept = Guid.NewGuid().ToString("N");

        await RaisedAsync(abandoned, commits: false, cancellationToken);

        Assert.Equal((0, 0), await WrittenAsync(abandoned));
        Assert.Equal(0, await CarriedAsync(cancellationToken));
        Assert.Equal(before, mail.Taken.Count(taken => taken.Destination.Value == Operator));

        await RaisedAsync(kept, commits: true, cancellationToken);

        Assert.Equal((1, 1), await WrittenAsync(kept));
        Assert.Equal(before, mail.Taken.Count(taken => taken.Destination.Value == Operator));
        Assert.Equal(1, await CarriedAsync(cancellationToken));
        Assert.Equal(0, await CarriedAsync(cancellationToken));
        Assert.Equal(before + 1, mail.Taken.Count(taken => taken.Destination.Value == Operator));
        Assert.Equal((0, 1), await WrittenAsync(kept));
    }

    // One condition raised inside a unit of work the caller began, as an operation
    // raises one, which the caller then commits or abandons.
    private async Task RaisedAsync(string scope, bool commits, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope services = host.Services.CreateAsyncScope();
        IUnitOfWork work = services.ServiceProvider.GetRequiredService<IUnitOfWork>();
        DateTimeOffset now = services.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        _ = await work.BeginAsync(cancellationToken);

        Result raised = await services.ServiceProvider.GetRequiredService<IAlertChannels>()
            .RaiseAsync(Alerts.Scoped(AlertCondition.RestrictionGranted, scope, now), cancellationToken);

        Assert.Null(raised.Match(() => (Error?)null, refusal => refusal));

        if (commits)
        {
            _ = await work.CommitAsync(cancellationToken);
        }
        else
        {
            await work.RollbackAsync();
        }
    }

    private async Task<int> CarriedAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope services = host.Services.CreateAsyncScope();

        return (await services.ServiceProvider.GetRequiredService<AlertDispatch>()
                .CarryAsync(
                    AccessContext.Of(BackgroundJobs.All.Single(job => job.Name == AlertDispatch.Job).Principal),
                    cancellationToken))
            .Match(
                carried => carried,
                refusal => throw new Xunit.Sdk.XunitException($"The pass failed: {refusal.Code}."));
    }

    // How many rows wait for the alert channels, and how many events were written, for
    // the condition raised under one scope.
    private async Task<(int Waiting, int Announced)> WrittenAsync(string scope)
    {
        await using NpgsqlConnection connection = await host.OpenAsync();

        return (
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM identity.raised_alerts WHERE idempotency_key LIKE '%' || @Scope || '%'",
                new { Scope = scope }),
            await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM identity.events WHERE kind = 'AlertRaised' AND payload::text LIKE '%' || @Scope || '%'",
                new { Scope = scope }));
    }
}
