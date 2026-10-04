using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Janus.Authentication.Factors;
using Janus.Authentication.Sending;
using Janus.Authentication.Tests.Identifiers;
using Janus.Authentication.Tests.Sending;
using Janus.Core;
using Janus.Core.Configuration;
using Xunit;

namespace Janus.Authentication.Tests.Factors;

/// <summary>
/// The one reminder an old set of recovery codes gets (AUTH-FACT-008 AC5).
/// </summary>
[Trait("kind", "unit")]
public sealed class RecoveryCodeRemindersTests : IAsyncDisposable
{
    private static readonly AccessContext Sweeper = AccessContext.Of(
        SystemPrincipal.ForDeployment("recovery-code-reminder", "AUTH-FACT-008", SystemOperation.ExpirySweep));

    private const string Address = "person@example.test";
    private const string Number = "+441632960011";

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Year = TimeSpan.FromDays(365);

    private readonly RecoveryCodeStoreInMemory _sets = new();
    private readonly IdentifierDirectoryInMemory _identifiers = new();
    private readonly GovernedSendInMemory _notifications = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private readonly RandomNumberGenerator _randomness = RandomNumberGenerator.Create();

    /// <summary>
    /// A deployment that names the one language its notices are written in, as every
    /// deployment names at least one.
    /// </summary>
    public RecoveryCodeRemindersTests()
    {
        _notifications.Work = _work;
        _configuration.Set(Settings.NotificationLanguages, ["en"]);
    }

    private RecoveryCodeReminders Reminders =>
        new(_sets, _identifiers, _notifications, _configuration, _work, _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _work.DisposeAsync();
        _randomness.Dispose();
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: a set older than the reminder age produces one reminder, to
    /// every channel of the security-notice set, and no further reminder until the set
    /// is regenerated.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC5_AnOldSetRemindsItsOwnerOnceAsync()
    {
        SubjectId subject = Held();
        await IssuedAsync(subject);

        Assert.Equal(0, await RemindedAsync());
        Assert.Empty(_notifications.Sent);

        _clock.Advance(Year);

        Assert.Equal(1, await RemindedAsync());
        Assert.Equal(Noon + Year, (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.RemindedAt);
        Assert.Equal([Address], _notifications.Mail.Select(sent => sent.Destination.Canonical));
        Assert.Equal([Number], _notifications.Texts.Select(sent => sent.Destination.Canonical));
        Assert.All(_notifications.Sent, sent =>
        {
            Assert.Equal(MessageKind.RecoveryCodesReminder, sent.Message);
            Assert.Equal(RestrictionPurpose.Notification, sent.Purpose);
            Assert.Equal(subject, sent.Subject);
        });

        _clock.Advance(Year);

        Assert.Equal(0, await RemindedAsync());
        Assert.Equal(2, _notifications.Sent.Count);

        await IssuedAsync(subject);
        _clock.Advance(Year);

        Assert.Equal(1, await RemindedAsync());
        Assert.Equal(4, _notifications.Sent.Count);
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: the age is the deployment's <c>recovery.codes.reminder</c>, and
    /// a set younger than it is left alone.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC5_TheAgeIsWhatTheDeploymentConfiguresAsync()
    {
        _configuration.Set(Settings.RecoveryCodesReminder, TimeSpan.FromDays(200));
        SubjectId subject = Held();
        await IssuedAsync(subject);

        _clock.Advance(TimeSpan.FromDays(199));

        Assert.Equal(0, await RemindedAsync());

        _clock.Advance(TimeSpan.FromDays(1));

        Assert.Equal(1, await RemindedAsync());
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: an account that is not active is not reminded, and its set
    /// stays owed the reminder.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC5_AnAccountThatIsNotActiveIsNotRemindedAsync()
    {
        SubjectId subject = Held();
        await IssuedAsync(subject);
        _sets.Deactivate(subject);

        _clock.Advance(Year);

        Assert.Equal(0, await RemindedAsync());
        Assert.Empty(_notifications.Sent);
        Assert.Null((await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.RemindedAt);
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: a pass reminds every set that is due however many there are,
    /// and each once.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC5_EverySetDueIsRemindedOnceInOnePassAsync()
    {
        List<SubjectId> subjects = [.. Enumerable.Range(0, 250).Select(_ => Held())];

        foreach (SubjectId subject in subjects)
        {
            await IssuedAsync(subject);
        }

        _clock.Advance(Year);

        Assert.Equal(250, await RemindedAsync());
        Assert.Equal(0, await RemindedAsync());
        Assert.Equal(250, _work.Committed);
        Assert.Equal(500, _notifications.Sent.Count);
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: a set whose every notice was refused is not closed as
    /// reminded, so the reminder stays owed and the next pass that a channel takes it
    /// on closes it.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC5_ASetWhoseEveryNoticeIsRefusedStaysOwedAsync()
    {
        SubjectId subject = Held();

        await IssuedAsync(subject);

        _notifications.Refusal = Error.From(ErrorCodes.Throttled);
        _clock.Advance(Year);

        Assert.Equal(0, await RemindedAsync());
        Assert.Null((await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.RemindedAt);

        _notifications.Refusal = null;

        Assert.Equal(1, await RemindedAsync());
        Assert.Equal(
            _clock.GetUtcNow(),
            (await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.RemindedAt);
    }

    /// <summary>
    /// AUTH-FACT-008 AC5: a set whose account holds no channel a reminder can reach is
    /// closed as reminded, since no later pass could reach it either.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC5_ASetNoChannelCanReachIsClosedAsync()
    {
        var subject = SubjectId.New(_randomness);

        await IssuedAsync(subject);
        _clock.Advance(Year);

        Assert.Equal(1, await RemindedAsync());
        Assert.Empty(_notifications.Sent);
        Assert.NotNull((await _sets.FindAsync(subject, TestContext.Current.CancellationToken))!.RemindedAt);
    }

    /// <summary>
    /// AUTH-FACT-008 AC5, AUTH-ABUSE-004 AC12: a reminder is asked for by no request, so
    /// it counts under no source, and as a notice to a holder it answers to the
    /// notification restriction of its own destination alone. Twenty sets due together
    /// are therefore each reminded under the shipped restrictions, on both channels,
    /// where one shared source would have refused the eleventh text message.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTH_FACT_008_AC5_TwentySetsDueTogetherAreEachRemindedUnderTheShippedRestrictionsAsync()
    {
        var ledger = new SendLedgerInMemory { Work = _work };
        var outbox = new SendOutboxInMemory { Work = _work };
        var carrier = new SendCarrierInMemory();
        var events = new EventsInMemory();
        var gateway = new SmsTransportInMemory();

        _configuration.Set(Settings.AbuseSmsBalanceFloor, 0m);

        var send = new GovernedSend(
            new SendAdmission(
                _configuration,
                ledger,
                RestrictionKeySuppliers.None,
                new SmsBalance(_configuration, gateway, new SmsBalanceLedgerInMemory(), _work, events, _clock)),
            outbox,
            carrier,
            Considered.Nothing(_work, _clock),
            _configuration,
            _work,
            _clock,
            _randomness);

        var reminders = new RecoveryCodeReminders(_sets, _identifiers, send, _configuration, _work, _clock);

        for (int holder = 0; holder < 20; holder++)
        {
            var subject = SubjectId.New(_randomness);

            _ = _identifiers.Verified(subject, IdentifierKind.Email, $"person{holder}@example.test");
            _ = _identifiers.Verified(subject, IdentifierKind.Phone, $"+4416329600{holder:D2}");

            await IssuedAsync(subject);
        }

        _clock.Advance(Year);

        int reminded = (await reminders.RemindAsync(Sweeper, TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));

        Assert.Equal(20, reminded);
        Assert.Equal(40, outbox.Waiting.Count);
        Assert.Equal(20, outbox.Waiting.Count(waiting => waiting.Requested.Kind is SendKind.Sms));
        Assert.All(outbox.Waiting, waiting => Assert.Null(waiting.Requested.Source));
        Assert.All(ledger.Keys, key => Assert.Equal("notification.destination", key.Restriction));
        Assert.Equal(40, ledger.Keys.Count);
        Assert.Equal(40, carrier.Attempted.Count);
    }

    private SubjectId Held()
    {
        var subject = SubjectId.New(_randomness);

        _ = _identifiers.Verified(subject, IdentifierKind.Email, Address);
        _ = _identifiers.Verified(subject, IdentifierKind.Phone, Number);

        return subject;
    }

    private async ValueTask IssuedAsync(SubjectId subject) =>
        await _sets.ReplaceAsync(
            RecoveryCodeSet.Of(subject, [], _clock.GetUtcNow()),
            TestContext.Current.CancellationToken);

    private async ValueTask<int> RemindedAsync() =>
        (await Reminders.RemindAsync(Sweeper, TestContext.Current.CancellationToken))
            .Match(value => value, error => throw new InvalidOperationException(error.Code.ToString()));
}
