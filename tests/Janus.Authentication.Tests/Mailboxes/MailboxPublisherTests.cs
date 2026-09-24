using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
    /// address, and is not made again.
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
        Assert.Equal(reserved.Id.ToString(), raised.Details["mailbox"].GetString());
        Assert.DoesNotContain(raised.Details.Values, value => value.ToString().Contains(Address, StringComparison.Ordinal));
        Assert.NotNull(reserved.FailedAt);

        _server.Unreachable = false;
        _clock.Advance(TimeSpan.FromDays(1));
        _ = await PassAsync();

        Assert.Equal(2, _server.Received.Count);
        Assert.Null(_server.Hosts(Address));
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
        new(_mailboxes, server, _configuration, _events, _work, _clock, _randomness);

    private async Task<int> PassAsync() =>
        (await Publisher.PublishAsync(TestContext.Current.CancellationToken))
            .Match(count => count, error => throw new InvalidOperationException(error.Code.ToString()));

    private async Task<Mailbox> ReservedAsync()
    {
        var reserved = Mailbox.Reserved(Address, _clock.GetUtcNow());

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
}
