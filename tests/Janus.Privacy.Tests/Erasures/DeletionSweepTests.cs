using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Privacy.Tests.Outbox;
using Janus.Privacy.Tests.Requests;
using Xunit;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// One pass over the deletion windows: which accounts the clock has reached, what the
/// erasure carries to the subscribers, and what a cancellation inside the window
/// leaves for the pass to find (IDN-LIFE-014, IDN-ACCT-007, PRIV-RIGHT-005).
/// </summary>
[Trait("kind", "unit")]
public sealed class DeletionSweepTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Noura =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private readonly AccountStatesInMemory _accounts = new();
    private readonly SubjectEraserInMemory _eraser = new();
    private readonly OutboxStoreInMemory _outbox = new();
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly ConfigurationInMemory _configuration = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    private DeletionSweep Sweep =>
        new(_accounts, _eraser, _outbox, _audit, _configuration, _work, _clock);

    /// <summary>
    /// IDN-LIFE-014: the erasure runs when the window elapses and not before, so an
    /// account one minute short of its end is left standing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_014_TheErasureRunsWhenTheWindowElapsesAndNotBeforeAsync()
    {
        TimeSpan grace = Settings.AccountDeletionGrace.Default;

        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon - grace + TimeSpan.FromMinutes(1));

        Assert.Equal(0, await Sweep.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_eraser.Erased);

        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, await Sweep.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Ahmed, Assert.Single(_eraser.Erased).Subject);
    }

    /// <summary>
    /// IDN-ACCT-007 AC4: a window the subject cancelled is not reached by the pass,
    /// whatever the clock has done since.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_ACCT_007_AC4_ACancelledWindowIsNotReachedByThePassAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon);
        _accounts.Deletes(Noura, DeletionOrigin.Self, Noon);
        _accounts.Cancels(Noura);

        _clock.Advance(Settings.AccountDeletionGrace.Default);

        Assert.Equal(1, await Sweep.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Ahmed, Assert.Single(_eraser.Erased).Subject);
        Assert.Equal(AccountState.Active, _accounts.Of(Noura));
    }

    /// <summary>
    /// PRIV-RIGHT-005b: the erasure goes on the outbox in the transaction that made
    /// it true, carrying the subject and why it happened.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task PRIV_RIGHT_005b_TheErasureIsAnnouncedInTheSameTransactionAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon);

        _clock.Advance(Settings.AccountDeletionGrace.Default);

        _ = await Sweep.SweepAsync(TestContext.Current.CancellationToken);

        Delivery announced = Assert.Single(_outbox.Deliveries);

        Assert.Equal(Ahmed, announced.Subject);
        Assert.Equal(SubjectEventKind.ErasureRequested, announced.Kind);
        Assert.Equal(ErasureReason.ErasureRequest, announced.Reason);
        Assert.Equal(_clock.GetUtcNow(), announced.RaisedAt);

        // Both writes are in one transaction, which is what leaves no erased account
        // the subscribers were never told about.
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// IDN-LIFE-003: a takedown ends in the same erasure, and the subscribers are
    /// told which of the two reached them.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_ATakedownCarriesItsOwnReasonAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Takedown, Noon);

        _clock.Advance(Settings.AccountDeletionGrace.Default);

        _ = await Sweep.SweepAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ErasureReason.MinorTakedown, Assert.Single(_eraser.Erased).Reason);
        Assert.Equal(ErasureReason.MinorTakedown, Assert.Single(_outbox.Deliveries).Reason);
    }

    /// <summary>
    /// IDN-LIFE-003 AC5: a takedown is erased when <c>takedown.grace</c> elapses, not
    /// when the ordinary deletion window would, and an ordinary window begun at the
    /// same instant is left running.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003_AC5_ATakedownIsErasedWhenItsOwnWindowElapsesAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Takedown, Noon);
        _accounts.Deletes(Noura, DeletionOrigin.Self, Noon);

        _clock.Advance(Settings.TakedownGrace.Default - TimeSpan.FromMinutes(1));

        Assert.Equal(0, await Sweep.SweepAsync(TestContext.Current.CancellationToken));

        _clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(1, await Sweep.SweepAsync(TestContext.Current.CancellationToken));
        Assert.Equal(Ahmed, Assert.Single(_eraser.Erased).Subject);
        Assert.Equal(AccountState.Deleting, _accounts.Of(Noura));
    }

    /// <summary>
    /// IDN-LIFE-003a AC1: the erasure the takedown's window ends in writes the identity
    /// change with its erasures row and its outbox record, all in one transaction.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AC1_TheTakedownsErasureWritesItsRowAndDeliveryInOneTransactionAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Takedown, Noon);

        _clock.Advance(Settings.TakedownGrace.Default);

        Assert.Equal(1, await Sweep.SweepAsync(TestContext.Current.CancellationToken));

        Erasure erased = Assert.Single(_eraser.Erased);
        Delivery delivery = Assert.Single(_outbox.Deliveries);

        Assert.Equal((Ahmed, ErasureReason.MinorTakedown), (erased.Subject, erased.Reason));
        Assert.Equal((Ahmed, SubjectEventKind.ErasureRequested), (delivery.Subject, delivery.Kind));
        Assert.Equal((1, 1), (_work.Opened, _work.Committed));
    }

    /// <summary>
    /// IDN-AUD-001: what the pass did is written down against the account it was
    /// done to, with no acting person, because nobody acted.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_AUD_001_ThePassRecordsWhatItDidAndToWhomAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.OutOfBandRequest, Noon);

        _clock.Advance(Settings.AccountDeletionGrace.Default);

        _ = await Sweep.SweepAsync(TestContext.Current.CancellationToken);

        PrivacyAuditEntry recorded = Assert.Single(_audit.Entries);

        Assert.Equal("privacy.erasure.executed", recorded.Action.ToString());
        Assert.Equal(Ahmed, recorded.Subject);
        Assert.Null(recorded.Acting);
        Assert.Equal(_clock.GetUtcNow(), recorded.At);
        Assert.Equal("OutOfBandRequest", recorded.Details["deletingBy"].GetString());
    }

    /// <summary>
    /// IDN-LIFE-014: the window is the one the deployment configured, so shortening
    /// it brings an account the earlier pass left standing into reach.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_014_TheWindowIsTheOneTheDeploymentConfiguredAsync()
    {
        _configuration.Set(Settings.AccountDeletionGrace, TimeSpan.FromDays(30));
        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon);

        _clock.Advance(TimeSpan.FromDays(14));

        Assert.Equal(0, await Sweep.SweepAsync(TestContext.Current.CancellationToken));

        _configuration.Set(Settings.AccountDeletionGrace, TimeSpan.FromDays(7));

        Assert.Equal(1, await Sweep.SweepAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// IDN-LIFE-014: each account is its own transaction, so a pass over several of
    /// them erases whole accounts and leaves none half done.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_014_EachAccountIsItsOwnTransactionAsync()
    {
        _accounts.Deletes(Ahmed, DeletionOrigin.Self, Noon);
        _accounts.Deletes(Noura, DeletionOrigin.Self, Noon.AddHours(1));

        _clock.Advance(Settings.AccountDeletionGrace.Default + TimeSpan.FromHours(1));

        Assert.Equal(2, await Sweep.SweepAsync(TestContext.Current.CancellationToken));

        Assert.Equal<IEnumerable<SubjectId>>(
            [Ahmed, Noura],
            [.. _eraser.Erased.Select(erasure => erasure.Subject)]);

        Assert.Equal(2, _work.Committed);
    }
}
