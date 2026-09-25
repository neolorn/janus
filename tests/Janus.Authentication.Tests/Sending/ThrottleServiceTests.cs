using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Sending;

/// <summary>
/// The progressive delay in front of every attempt: what failures earn, what time
/// forgives, what the account component is capped at, and what a browser the account
/// already knows is exempt from (AUTH-ABUSE-001, AUTH-ABUSE-002, OPS-ALERT-001).
/// </summary>
[Trait("kind", "unit")]
public sealed class ThrottleServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly ThrottleTerms Shipped = new(
        Enabled: true,
        Threshold: 3,
        Initial: TimeSpan.FromSeconds(1),
        Factor: 2.0m,
        Maximum: TimeSpan.FromSeconds(60),
        AccountCap: TimeSpan.FromSeconds(30),
        Decay: TimeSpan.FromMinutes(10));

    private readonly ConfigurationInMemory _configuration = new();
    private readonly ThrottleLedgerInMemory _ledger = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly EventsInMemory _events = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    private ThrottleService Service => new(_configuration, _ledger, _work, _events, _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC1: failures buy time, they do not disable anything. The
    /// delay rises and every further attempt is still taken.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_RepeatedFailuresRaiseTheDelayAndDisableNothingAsync()
    {
        var attempt = new ThrottleAttempt("198.51.100.7", "someone@example.test");

        Assert.Equal(TimeSpan.Zero, await DelayAsync(attempt));

        await FailedAsync(attempt, times: 3);
        TimeSpan first = await DelayAsync(attempt);

        await FailedAsync(attempt, times: 2);
        TimeSpan later = await DelayAsync(attempt);

        Assert.Equal(TimeSpan.FromSeconds(1), first);
        Assert.Equal(TimeSpan.FromSeconds(4), later);
        Assert.True(later < TimeSpan.MaxValue);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC2: what was accumulated halves once per half-life, so an
    /// account left alone is not still held an hour later.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC2_TheDelayDecaysWithTimeAsync()
    {
        var attempt = new ThrottleAttempt("198.51.100.7", "someone@example.test");

        await FailedAsync(attempt, times: 6);

        Assert.Equal(TimeSpan.FromSeconds(8), await DelayAsync(attempt));

        _clock.Advance(TimeSpan.FromMinutes(20));

        Assert.Equal(TimeSpan.Zero, await DelayAsync(attempt));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC2: a failure after a quiet spell earns what the halved count
    /// earns, not what the count before the spell would have.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC2_AFailureAfterAQuietSpellEarnsLessAsync()
    {
        var attempt = new ThrottleAttempt("198.51.100.7", "someone@example.test");

        await FailedAsync(attempt, times: 6);
        _clock.Advance(TimeSpan.FromMinutes(20));
        await FailedAsync(attempt, times: 1);

        Assert.Equal(TimeSpan.FromSeconds(1), await DelayAsync(attempt));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC1: failures made one after another, each once the delay the
    /// last one earned has run, escalate the delay; seconds of decay between them do
    /// not keep the count where it was.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_FailuresMadeOneAfterAnotherEscalateTheDelayAsync()
    {
        var attempt = new ThrottleAttempt("198.51.100.7", "someone@example.test");
        var earned = new List<TimeSpan>();

        for (int failure = 0; failure < 6; failure++)
        {
            await FailedAsync(attempt, times: 1);

            TimeSpan delay = await DelayAsync(attempt);

            earned.Add(delay);
            _clock.Advance(delay + TimeSpan.FromSeconds(1));
        }

        Assert.Equal<TimeSpan>(
            [
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(4),
                TimeSpan.FromSeconds(8),
            ],
            earned);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC1 and AUTH-ABUSE-002 AC2: the delay runs from the failure that
    /// earned it, so what is left shrinks as the clock runs and an attempt made once it
    /// has run is looked at.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC1_TheDelayRunsFromTheFailureThatEarnedItAsync()
    {
        var attempt = new ThrottleAttempt("198.51.100.7", "someone@example.test");

        await FailedAsync(attempt, times: 4);

        TimeSpan earned = await DelayAsync(attempt);

        _clock.Advance(TimeSpan.FromSeconds(0.5));

        TimeSpan left = await DelayAsync(attempt);

        _clock.Advance(TimeSpan.FromSeconds(1.5));

        Assert.Equal(TimeSpan.FromSeconds(2), earned);
        Assert.Equal(TimeSpan.FromSeconds(1.5), left);
        Assert.Equal(TimeSpan.Zero, await DelayAsync(attempt));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC3: an attack spread across addresses raises no source
    /// counter above the threshold, and is caught by the account component.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC3_ADistributedAttackIsCaughtByTheAccountComponentAsync()
    {
        var account = SubjectId.New(_randomness);

        for (int source = 0; source < 5; source++)
        {
            await FailedAsync(
                new ThrottleAttempt("198.51.100." + source, "someone@example.test")
                {
                    Account = account,
                },
                times: 1);
        }

        TimeSpan held = await DelayAsync(
            new ThrottleAttempt("203.0.113.9", "someone@example.test") { Account = account });

        Assert.Equal(TimeSpan.FromSeconds(4), held);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC4: however many attempts are made, the account component
    /// stops at its cap, so the lever an attacker holds stays small.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC4_TheAccountDelayStopsAtItsCapAsync()
    {
        var account = SubjectId.New(_randomness);

        for (int source = 0; source < 30; source++)
        {
            await FailedAsync(
                new ThrottleAttempt("198.51.100." + source, null) { Account = account },
                times: 1);
        }

        Assert.Equal(
            TimeSpan.FromSeconds(30),
            await DelayAsync(new ThrottleAttempt("203.0.113.9", null) { Account = account }));
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC5: a browser the account already knows answers to the source
    /// component alone, so an attack from elsewhere does not hold its owner out.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_001_AC5_ARecognisedBrowserIsNotHeldByAnAttackAsync()
    {
        var account = SubjectId.New(_randomness);

        for (int source = 0; source < 30; source++)
        {
            await FailedAsync(
                new ThrottleAttempt("198.51.100." + source, "someone@example.test")
                {
                    Account = account,
                },
                times: 1);
        }

        TimeSpan returning = await DelayAsync(
            new ThrottleAttempt("203.0.113.9", "someone@example.test")
            {
                Account = account,
                Recognised = true,
            });

        Assert.Equal(TimeSpan.Zero, returning);
    }

    /// <summary>
    /// AUTH-ABUSE-001 AC6: the identifier component answers to the account
    /// component's cap and has no key of its own.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_001_AC6_TheIdentifierComponentSharesTheAccountTerms()
    {
        Assert.Equal(
            Throttle.Cap(ThrottleScope.Account, Shipped),
            Throttle.Cap(ThrottleScope.Identifier, Shipped));

        Assert.Equal(Shipped.AccountCap, Throttle.Cap(ThrottleScope.Identifier, Shipped));

        Assert.DoesNotContain(
            typeof(Settings)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Select(property => (property.GetValue(null) as Setting)?.Key.ToString())
                .Where(key => key is not null),
            key => key!.StartsWith("abuse.throttle.identifier", StringComparison.Ordinal));
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC1: the same failures against an identifier an account holds
    /// and one it does not earn the same delay, because nothing on this path asks
    /// which it is.
    /// </summary>
    [Fact]
    public async Task AUTH_ABUSE_002_AC1_TheDelayIsTheSameWhetherTheAccountExistsOrNotAsync()
    {
        var account = SubjectId.New(_randomness);
        var held = new ThrottleAttempt("198.51.100.7", "someone@example.test") { Account = account };
        var unheld = new ThrottleAttempt("198.51.100.8", "nobody@example.test");

        await FailedAsync(held, times: 5);
        await FailedAsync(unheld, times: 5);

        Assert.Equal(await DelayAsync(unheld), await DelayAsync(held));
    }

    /// <summary>
    /// AUTH-ABUSE-002 AC2: the refusal says when the next attempt is looked at, as the
    /// one <c>retryAt</c> every throttle of the library answers with, and nothing else.
    /// </summary>
    [Fact]
    public void AUTH_ABUSE_002_AC2_TheRemainingDelayIsCommunicated()
    {
        DateTimeOffset lifts = Noon.AddSeconds(4.2);

        Error refusal = ThrottleService.Refusal(lifts);

        Assert.Equal(ErrorCodes.Throttled, refusal.Code);
        Assert.Equal("retryAt", Assert.Single(refusal.Details).Key);
        Assert.Equal(lifts, Assert.Single(refusal.Details).Value.GetDateTimeOffset());
    }

    /// <summary>
    /// OPS-ALERT-001 AC1: sustained failures against one account raise the alert
    /// with no one watching for them.
    /// </summary>
    [Fact]
    public async Task OPS_ALERT_001_AC1_SustainedFailuresAgainstOneAccountRaiseTheAlertAsync()
    {
        _configuration.Set(Settings.AlertingAuthFailuresThreshold, 4);

        var attempt = new ThrottleAttempt("198.51.100.7", null)
        {
            Account = SubjectId.New(_randomness),
        };

        await FailedAsync(attempt, times: 3);

        Assert.Empty(_events.Of<AlertRaised>());

        await FailedAsync(attempt, times: 1);

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.AuthFailuresSustained, raised.Condition);
        Assert.Equal(AlertSeverity.High, raised.Severity);
    }

    /// <summary>
    /// A sign-in that succeeds forgets what the attempt accumulated, so the person
    /// who got it right is not held by their own earlier typing.
    /// </summary>
    [Fact]
    public async Task SucceededAsync_AnAttemptThatSucceeded_ForgetsWhatItAccumulatedAsync()
    {
        var attempt = new ThrottleAttempt("198.51.100.7", "someone@example.test");

        await FailedAsync(attempt, times: 5);
        await Service.SucceededAsync(attempt, TestContext.Current.CancellationToken);

        Assert.Empty(_ledger.Counted);
        Assert.Equal(TimeSpan.Zero, await DelayAsync(attempt));
    }

    /// <summary>
    /// The delay is the largest any scope earns, never their sum, so three counters
    /// do not multiply one person's wait.
    /// </summary>
    [Fact]
    public async Task DelayAsync_SeveralScopesCounted_AnswersTheLargestAsync()
    {
        var attempt = new ThrottleAttempt("198.51.100.7", "someone@example.test")
        {
            Account = SubjectId.New(_randomness),
        };

        await FailedAsync(attempt, times: 5);

        Assert.Equal(TimeSpan.FromSeconds(4), await DelayAsync(attempt));
    }

    private async Task<TimeSpan> DelayAsync(ThrottleAttempt attempt) =>
        (await Service.DelayAsync(attempt, TestContext.Current.CancellationToken)).Match(
            delay => delay,
            error => throw new Xunit.Sdk.XunitException($"The delay was refused: {error.Code}."));

    private async Task FailedAsync(ThrottleAttempt attempt, int times)
    {
        for (int failure = 0; failure < times; failure++)
        {
            (await Service.FailedAsync(attempt, TestContext.Current.CancellationToken)).Switch(
                () => { },
                error => throw new Xunit.Sdk.XunitException($"The failure was refused: {error.Code}."));
        }
    }
}
