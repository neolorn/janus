using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Authentication.Mailboxes;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Mailboxes;

/// <summary>
/// The outbox publisher's pass over the mailboxes: what it pushes, under which key,
/// and what it does when the mail server does not answer (INT-MAIL-006, INT-MAIL-006a,
/// INT-MAIL-007, INT-MAIL-009).
/// </summary>
[Trait("kind", "unit")]
public sealed class MailboxPublisherTests : IAsyncDisposable
{
    private const string Address = "staff@example.test";

    private static readonly DateTimeOffset Noon = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    private readonly MailboxStoreInMemory _mailboxes = new();
    private readonly MailServerInMemory _server = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly EventsInMemory _events = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();
    private readonly SubjectId _holder;

    /// <summary>
    /// A person whose account and membership stand.
    /// </summary>
    public MailboxPublisherTests()
    {
        _holder = SubjectId.New(_randomness);
        _ = _mailboxes.Standing.Add(_holder);
    }

    private MailboxPublisher Publisher => Built(_server);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// INT-MAIL-006 AC1c: a mailbox reserved for an invitation is created on the
    /// server disabled, before anyone holds it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006_AC1c_AReservedMailboxIsCreatedDisabledAsync()
    {
        Mailbox reserved = await ReservedAsync();

        Assert.Equal(1, await PassAsync());

        Assert.False(_server.Hosts(Address));
        Assert.Equal(MailboxState.Disabled, reserved.Pushed);
        Assert.Null(reserved.Pending);
    }

    /// <summary>
    /// INT-MAIL-006a AC1: once the holder's suspension has committed, the first pass
    /// pushes the disabled state, and nothing had to tell the publisher.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006a_AC1_ASuspendedHolderIsDisabledOnTheFirstPassAsync()
    {
        Mailbox held = await HeldAsync();

        Assert.True(_server.Hosts(Address));

        _ = _mailboxes.Standing.Remove(_holder);

        Assert.Equal(1, await PassAsync());

        Assert.False(_server.Hosts(Address));
        Assert.Equal(MailboxState.Disabled, held.Pushed);
    }

    /// <summary>
    /// INT-MAIL-007 AC1: a push the server applied but whose answer was lost is made
    /// again under the same key, and the server applies it once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC1_APushMadeAgainProducesNothingTwiceAsync()
    {
        _ = await ReservedAsync();

        _server.LosesAnswers = true;

        Assert.Equal(0, await PassAsync());

        _server.LosesAnswers = false;
        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, await PassAsync());

        Assert.Equal(2, _server.Received.Count);
        Assert.Equal(_server.Received[0].Key, _server.Received[1].Key);
        Assert.Single(_server.Applied);
    }

    /// <summary>
    /// INT-MAIL-007 AC1: a push is written down under its key, and committed, before it
    /// reaches the server, so a process that stops once the server has applied it finds
    /// it outstanding under the same key on its return.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC1_APushIsWrittenDownBeforeItLeavesAsync()
    {
        _ = await ReservedAsync();

        (int Committed, Guid? Key)? written = null;

        _server.Receiving = _ => written = (_work.Committed, _mailboxes.Keys.LastOrDefault());

        _ = await PassAsync();

        Assert.Equal((1, _server.Received[0].Key), written);
    }

    /// <summary>
    /// INT-MAIL-007 AC1: a push whose answer was lost may have been applied, so a
    /// return to the state last confirmed while it is outstanding is pushed again under
    /// a key of its own, and the server ends in the state owed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC1_AReturnWhileAPushIsOutstandingIsPushedAgainAsync()
    {
        Mailbox held = await HeldAsync();

        _ = _mailboxes.Standing.Remove(_holder);
        _server.LosesAnswers = true;

        _ = await PassAsync();

        Assert.False(_server.Hosts(Address));

        Guid disabling = _server.Received[^1].Key;

        _ = _mailboxes.Standing.Add(_holder);
        _server.LosesAnswers = false;

        Assert.Equal(1, await PassAsync());

        Assert.True(_server.Hosts(Address));
        Assert.Equal(MailboxState.Enabled, held.Pushed);
        Assert.Null(held.Pending);
        Assert.NotEqual(disabling, _server.Received[^1].Key);
    }

    /// <summary>
    /// INT-MAIL-007 AC3 and OPS-OBS-002: a push the server never takes backs off,
    /// spends its budget, raises <c>degradation</c> naming the mailbox and not its
    /// address, and is not made again within the day.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC3_APushThatSpendsItsBudgetIsVisibleAsync()
    {
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 2);

        Mailbox reserved = await ReservedAsync();

        _server.Unreachable = true;

        _ = await PassAsync();

        Assert.Empty(_events.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromHours(1));
        _ = await PassAsync();

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.Equal("degradation:mailbox.push:" + reserved.Id, Alerts.Deduplication(raised.IdempotencyKey));
        Assert.Equal(reserved.Id.ToString(), raised.Details["mailbox"].GetString());
        Assert.Equal("disabled", raised.Details["state"].GetString());
        Assert.Equal(2, raised.Details["attempts"].GetInt32());
        Assert.DoesNotContain(raised.Details.Values, value => value.ToString().Contains(Address, StringComparison.Ordinal));
        Assert.NotNull(reserved.FailedAt);

        _server.Unreachable = false;
        _clock.Advance(TimeSpan.FromHours(23));
        _ = await PassAsync();

        Assert.Equal(2, _server.Received.Count);
        Assert.Null(_server.Hosts(Address));
    }

    /// <summary>
    /// INT-MAIL-001 AC4, D-177: a push the server answers with a conflict is marked
    /// failed at that attempt, with no further attempt in the run, and raises
    /// <c>degradation</c> scoped to the mailbox's conflict, naming the mailbox and the
    /// state and never the address, in the transaction that records it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_001_AC4_AConflictIsMarkedFailedAndRaisedOnItsFirstAttemptAsync()
    {
        _server.Set(Address, enabled: true, MailboxId.Of(Noon.AddDays(-30)));

        Mailbox reserved = await ReservedAsync();
        int committed = _work.Committed;

        Assert.Equal(0, await PassAsync());

        AlertRaised raised = Assert.Single(_events.Of<AlertRaised>());

        Assert.Equal(AlertCondition.Degradation, raised.Condition);
        Assert.Equal("degradation:mailbox.conflict:" + reserved.Id, Alerts.Deduplication(raised.IdempotencyKey));
        Assert.Equal(["mailbox", "state"], raised.Details.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(reserved.Id.ToString(), raised.Details["mailbox"].GetString());
        Assert.Equal("disabled", raised.Details["state"].GetString());
        Assert.Equal(Noon, reserved.FailedAt);
        Assert.Equal(1, reserved.Attempts);
        Assert.Equal(committed + 2, _work.Committed);

        _clock.Advance(TimeSpan.FromHours(1));
        _ = await PassAsync();

        Assert.Single(_server.Received);
        Assert.True(_server.Hosts(Address));
    }

    /// <summary>
    /// INT-MAIL-007 AC6, D-177: a push marked failed, whether its attempts were spent or
    /// the server answered with a conflict, is begun again under the same key a day
    /// after it was last marked failed; a run that ends failed raises its alert again,
    /// and once the cause is gone the server reaches the state owed with no action in
    /// the library.
    /// </summary>
    /// <param name="conflict">Whether the cause is a conflict rather than an unreachable server.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task INT_MAIL_007_AC6_APushMarkedFailedIsBegunAgainADayLaterAsync(bool conflict)
    {
        _configuration.Set(Settings.OutboxRetryMaxAttempts, 1);

        if (conflict)
        {
            _server.Set(Address, enabled: true, MailboxId.Of(Noon.AddDays(-30)));
        }
        else
        {
            _server.Unreachable = true;
        }

        Mailbox reserved = await ReservedAsync();

        _ = await PassAsync();

        Guid key = _server.Received[0].Key;

        Assert.Single(_events.Of<AlertRaised>());

        _clock.Advance(TimeSpan.FromDays(1));
        _ = await PassAsync();

        Assert.Equal(2, _server.Received.Count);
        Assert.Equal(key, _server.Received[1].Key);
        Assert.Equal(2, _events.Of<AlertRaised>().Count);
        Assert.Equal(
            Alerts.Deduplication(_events.Of<AlertRaised>()[0].IdempotencyKey),
            Alerts.Deduplication(_events.Of<AlertRaised>()[1].IdempotencyKey));

        // The cause is gone at the server: the account was resolved there, or the
        // server is reachable again.
        _server.Set(Address, enabled: null);
        _server.Unreachable = false;
        _clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(1, await PassAsync());

        Assert.Equal(key, _server.Received[^1].Key);
        Assert.False(_server.Hosts(Address));
        Assert.Equal(reserved.Id, _server.Carried(Address));
        Assert.Equal(MailboxState.Disabled, reserved.Pushed);
        Assert.Equal(2, _events.Of<AlertRaised>().Count);
    }

    /// <summary>
    /// INT-MAIL-007 AC1: each attempt is counted, and the count committed, before the
    /// attempt is made, so an attempt the process does not live to see answered is
    /// spent all the same.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_007_AC1_EachAttemptIsCountedBeforeItIsMadeAsync()
    {
        Mailbox reserved = await ReservedAsync();
        var seen = new List<(int Attempts, int Recorded, int Committed)>();

        _server.Unreachable = true;
        _server.Receiving = _ => seen.Add((reserved.Attempts, _mailboxes.Recorded, _work.Committed));

        _ = await PassAsync();
        _clock.Advance(TimeSpan.FromHours(1));
        _ = await PassAsync();

        Assert.Equal(new List<(int, int, int)> { (1, 1, 1), (2, 3, 3) }, seen);
    }

    /// <summary>
    /// INT-MAIL-007 AC7, D-177: a removal of a mailbox no push of which was ever
    /// attempted is confirmed without a call to the server; one of a mailbox a push of
    /// which was attempted is sent, since the server may hold it.
    /// </summary>
    /// <param name="attempted">Whether a push of the mailbox was attempted before its release.</param>
    /// <returns>The work of the test.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task INT_MAIL_007_AC7_ARemovalNeverAttemptedIsConfirmedUnsentAsync(bool attempted)
    {
        Mailbox reserved = await ReservedAsync();

        if (attempted)
        {
            _server.Unreachable = true;
            _ = await PassAsync();
            _server.Unreachable = false;
        }

        reserved.Release(_clock.GetUtcNow());
        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, await PassAsync());

        Assert.Equal(MailboxState.Removed, reserved.Pushed);
        Assert.Null(reserved.Pending);
        Assert.Equal(attempted ? 2 : 0, _server.Received.Count);
        Assert.Equal(attempted ? (MailboxState?)MailboxState.Removed : null, _server.Received.LastOrDefault()?.State);
    }

    /// <summary>
    /// INT-MAIL-006a: a change of the state owed while a push is outstanding begins a
    /// push of its own, so the last state owed is the one the server ends in.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_006a_TheLastStateOwedIsTheOneTheServerEndsInAsync()
    {
        Mailbox reserved = await ReservedAsync();

        _server.Unreachable = true;

        _ = await PassAsync();

        Guid creating = _server.Received[^1].Key;

        reserved.Hold(_holder);
        _server.Unreachable = false;

        Assert.Equal(1, await PassAsync());

        Assert.True(_server.Hosts(Address));
        Assert.Equal(MailboxState.Enabled, reserved.Pushed);
        Assert.NotEqual(creating, _server.Received[^1].Key);
    }

    /// <summary>
    /// INT-MAIL-009 AC1 and INT-MAIL-008 AC2: mailbox hosting is a registration of its
    /// own, and a deployment that makes none pushes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_009_AC1_MailboxHostingIsARegistrationOfItsOwnAsync()
    {
        Mailbox reserved = await ReservedAsync();

        int confirmed = (await Built(server: null).PublishAsync(TestContext.Current.CancellationToken))
            .Match(count => count, _ => -1);

        Assert.Equal(0, confirmed);
        Assert.Null(reserved.Pushed);
        Assert.Equal(0, _mailboxes.Recorded);
    }

    /// <summary>
    /// INT-MAIL-008 AC2: another mail server is another registration of the same port,
    /// and the publisher pushes to whichever the deployment registered, unchanged.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task INT_MAIL_008_AC2_AnotherMailServerIsARegistrationAndNoCodeChangeAsync()
    {
        var another = new MailServerInMemory();
        Mailbox reserved = await ReservedAsync();

        int pushed = (await Built(another).PublishAsync(TestContext.Current.CancellationToken))
            .Match(count => count, _ => -1);

        Assert.Equal(1, pushed);
        Assert.Single(another.Received);
        Assert.Empty(_server.Received);
        Assert.Equal(MailboxState.Disabled, reserved.Pushed);
    }

    /// <summary>
    /// A settled mailbox is not written again, so a pass over a quiet directory costs
    /// one read.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PublishAsync_ASettledMailbox_IsLeftAloneAsync()
    {
        _ = await HeldAsync();

        int recorded = _mailboxes.Recorded;

        Assert.Equal(0, await PassAsync());
        Assert.Equal(recorded, _mailboxes.Recorded);
        Assert.Equal(2, _server.Received.Count);
    }

    private MailboxPublisher Built(IMailServer? server) =>
        new(_mailboxes, new MailServerInUseInMemory(server), _configuration, _events, _work, _clock, _randomness);

    private async Task<int> PassAsync() =>
        (await Publisher.PublishAsync(TestContext.Current.CancellationToken))
            .Match(count => count, error => throw new InvalidOperationException(error.Code.ToString()));

    private async Task<Mailbox> ReservedAsync()
    {
        var reserved = Mailbox.Reserved(Parsed(Address), _clock.GetUtcNow());

        await _mailboxes.AddAsync(reserved, TestContext.Current.CancellationToken);

        return reserved;
    }

    // A mailbox created, then held by a standing member, as acknowledgement leaves it.
    private async Task<Mailbox> HeldAsync()
    {
        Mailbox reserved = await ReservedAsync();

        _ = await PassAsync();

        reserved.Hold(_holder);

        _ = await PassAsync();

        return _mailboxes.Held.Single();
    }

    private static EmailAddress Parsed(string value)
    {
        Assert.True(EmailAddress.TryParse(value, out EmailAddress address));

        return address;
    }
}
