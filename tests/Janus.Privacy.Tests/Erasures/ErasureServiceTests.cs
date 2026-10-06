using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;
using Janus.Privacy.Policies;
using Janus.Privacy.Tests.Exports;
using Janus.Privacy.Tests.Outbox;
using Janus.Privacy.Tests.Requests;
using Xunit;

namespace Janus.Privacy.Tests.Erasures;

/// <summary>
/// The erasures an operator works: every one still outstanding, how far each
/// subscriber has got with one, and the manual completion of one whose retries were
/// spent (IDN-LIFE-003a, IDN-LIFE-003b, chapter 09 section 8a).
/// </summary>
[Trait("kind", "unit")]
public sealed class ErasureServiceTests : IAsyncDisposable
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly SubjectId Ahmed =
        new(Guid.Parse("11111111-1111-4111-8111-111111111111"));

    private static readonly SubjectId Mona =
        new(Guid.Parse("22222222-2222-4222-8222-222222222222"));

    private static readonly SubjectId Sara =
        new(Guid.Parse("55555555-5555-4555-8555-555555555555"));

    private static readonly SubjectId Omar =
        new(Guid.Parse("66666666-6666-4666-8666-666666666666"));

    private static readonly OrganizationId Company =
        new(Guid.Parse("33333333-3333-4333-8333-333333333333"));

    private static readonly SessionId Browser =
        new(Guid.Parse("44444444-4444-4444-8444-444444444444"));

    private readonly AccessGateInMemory _gate = new();
    private readonly AdministrativeOrganizationInMemory _administrative = new();
    private readonly StepUpGateInMemory _stepUp = new();
    private readonly OutboxStoreInMemory _outbox = new();
    private readonly ErasureStoreInMemory _erasures = new();
    private readonly SubscriberInMemory _records = new("records", required: true);
    private readonly SubscriberInMemory _newsletter = new("newsletter", required: false);
    private readonly PrivacyAuditInMemory _audit = new();
    private readonly UnitOfWorkInMemory _work = new();
    private readonly FixedClock _clock = new(Noon);
    private ErasureLedgerInMemory? _ledger;

    /// <summary>
    /// A deployment with one member of staff who holds <c>privacyrequest:manage</c>.
    /// </summary>
    public ErasureServiceTests()
    {
        _administrative.Organization = Company;
        _gate.Grant(Mona, Company, Permissions.PrivacyRequestManage);
    }

    private ErasureService Erasures =>
        new(
            new AdministrativeScope(_gate, _administrative),
            _stepUp,
            _outbox,
            _erasures,
            [_records, _newsletter],
            _ledger,
            _audit,
            _work,
            _clock);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await _work.DisposeAsync();

    /// <summary>
    /// IDN-LIFE-003b AC2: every erasure whose host-side work is outstanding is listed,
    /// awaiting subscribers or failed, oldest first, and one every required subscriber
    /// confirmed is not; a takedown's delivery is not an erasure.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003b_AC2_EveryIncompleteErasureIsListedAsync()
    {
        Delivery failed = await ErasedAsync(Sara, Noon.AddHours(-2), ErasureStatus.Failed);
        Delivery awaiting = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.AwaitingSubscribers);
        _ = await ErasedAsync(Omar, Noon.AddHours(-3), ErasureStatus.Complete);
        await _outbox.AddAsync(
            Delivery.Of(Mona, SubjectEventKind.TakedownExecuted, Noon),
            TestContext.Current.CancellationToken);

        IReadOnlyList<ErasureProgress> listed = Held(await Erasures.ListAsync(
            AccessContext.Of(Mona),
            TestContext.Current.CancellationToken));

        Assert.Equal([failed.Id.Value, awaiting.Id.Value], listed.Select(erasure => erasure.Id.Value));
        Assert.Equal([Sara, Ahmed], listed.Select(erasure => erasure.Subject));
        Assert.All(listed, erasure => Assert.Equal(["records", "newsletter"], erasure.Subscribers.Select(subscriber => subscriber.Name)));
    }

    /// <summary>
    /// IDN-LIFE-003b, IDN-LIFE-003a: one erasure reads with its reason, status and
    /// attempts, and with each registered subscriber, whether it is required, and when
    /// it confirmed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003b_OneErasureIsReadWithEachSubscribersConfirmationAsync()
    {
        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);

        _outbox.Confirms(delivery, "newsletter", Noon.AddMinutes(-30));

        ErasureProgress progress = Held(await ReadAsync(Mona, new ErasureId(delivery.Id.Value)));

        Assert.Equal(delivery.Id.Value, progress.Id.Value);
        Assert.Equal(Ahmed, progress.Subject);
        Assert.Equal(ErasureReason.MinorTakedown, progress.Reason);
        Assert.Equal(ErasureStatus.Failed, progress.Status);
        Assert.Equal(delivery.Attempts, progress.Attempts);
        Assert.Equal(
            [
                new SubscriberConfirmation("records", Required: true, ConfirmedAt: null),
                new SubscriberConfirmation("newsletter", Required: false, Noon.AddMinutes(-30)),
            ],
            progress.Subscribers);
    }

    /// <summary>
    /// IDN-LIFE-003b: an identifier that names no erasure, including the identifier of
    /// a takedown's delivery, reads as no erasure.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003b_AnIdentifierThatNamesNoErasureIsNotFoundAsync()
    {
        var takedown = Delivery.Of(Ahmed, SubjectEventKind.TakedownExecuted, Noon);

        await _outbox.AddAsync(takedown, TestContext.Current.CancellationToken);

        Assert.Equal(
            ErrorCodes.ErasureNotFound,
            Refused(await ReadAsync(Mona, new ErasureId(Guid.NewGuid()))).Code);
        Assert.Equal(
            ErrorCodes.ErasureNotFound,
            Refused(await ReadAsync(Mona, new ErasureId(takedown.Id.Value))).Code);
    }

    /// <summary>
    /// IDN-LIFE-003a: the manual path closes an erasure whose retries were spent, and
    /// its erasures row with it, in one transaction that records who closed it and
    /// which required subscribers had not confirmed.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AManualCompletionClosesAFailedErasureAndIsRecordedAsync()
    {
        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);
        var erasure = new ErasureId(delivery.Id.Value);

        Held(await CompletedAsync(Mona, erasure));

        PrivacyAuditEntry entry = Assert.Single(_audit.Entries);

        Assert.Equal(ErasureStatus.Complete, delivery.Status);
        Assert.Equal(ErasureStatus.Complete, Assert.Single(_erasures.Erasures).Status);
        Assert.Equal(AuditActions.ErasureCompleted, entry.Action);
        Assert.Equal(Mona, entry.Acting);
        Assert.Equal(Ahmed, entry.Subject);
        Assert.Equal(Noon, entry.At);
        Assert.Equal(erasure.ToString(), entry.Details["erasure"].GetString());
        Assert.Equal("erasure-requested", entry.Details["kind"].GetString());
        Assert.Equal(
            ["records"],
            entry.Details["outstanding"].EnumerateArray().Select(name => name.GetString()));
        Assert.Equal(1, _work.Opened);
        Assert.Equal(1, _work.Committed);
        Assert.Contains((Mona, Browser, StepUpAction.ErasureComplete), _stepUp.Asked);
    }

    /// <summary>
    /// IDN-LIFE-003a, CONV-DESIGN-003: an erasure another operator closed while this
    /// completion waited for its row is refused as no longer failed, and nothing more
    /// is recorded.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AnErasureClosedMeanwhileIsNotClosedTwiceAsync()
    {
        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);

        _outbox.Locking = held =>
        {
            _outbox.Locking = null;
            held.CompleteManually();
        };

        Error refused = Refused(await CompletedAsync(Mona, new ErasureId(delivery.Id.Value)));

        Assert.Equal(ErrorCodes.ErasureNotFailed, refused.Code);
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
        Assert.Empty(_audit.Entries);
        Assert.False(_work.Open);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);
    }

    /// <summary>
    /// IDN-LIFE-003a: the manual path is for permanent failure, so an erasure the
    /// subscribers are still working through, or one already complete, is refused and
    /// nothing is recorded; the step-up is not asked for a delivery the path would not
    /// close (X8).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AManualCompletionRefusesAnErasureThatNeverFailedAsync()
    {
        Delivery awaiting = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.AwaitingSubscribers);
        Delivery complete = await ErasedAsync(Sara, Noon.AddHours(-2), ErasureStatus.Complete);

        Assert.Equal(
            ErrorCodes.ErasureNotFailed,
            Refused(await CompletedAsync(Mona, new ErasureId(awaiting.Id.Value))).Code);
        Assert.Equal(
            ErrorCodes.ErasureNotFailed,
            Refused(await CompletedAsync(Mona, new ErasureId(complete.Id.Value))).Code);
        Assert.Equal(ErasureStatus.AwaitingSubscribers, awaiting.Status);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _work.Opened);
        Assert.Empty(_stepUp.Asked);
    }

    /// <summary>
    /// IDN-LIFE-003a: a takedown's failed delivery is closed by hand, named by its
    /// <c>takedownId</c>, and recorded with its kind; no erasures row or ledger line is
    /// touched, since it is no erasure's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AFailedTakedownDeliveryIsCompletedByHandAsync()
    {
        _ledger = new ErasureLedgerInMemory();

        Delivery takedown = await FailedAsync(SubjectEventKind.TakedownExecuted);

        Held(await CompletedAsync(Mona, new ErasureId(takedown.Id.Value)));

        PrivacyAuditEntry entry = Assert.Single(_audit.Entries);

        Assert.Equal(ErasureStatus.Complete, takedown.Status);
        Assert.Empty(_erasures.Erasures);
        Assert.Empty(_ledger.Lines);
        Assert.DoesNotContain("erasure-ledger", takedown.Confirmed);
        Assert.Equal(AuditActions.ErasureCompleted, entry.Action);
        Assert.Equal(Ahmed, entry.Subject);
        Assert.Equal(new ErasureId(takedown.Id.Value).ToString(), entry.Details["erasure"].GetString());
        Assert.Equal("takedown-executed", entry.Details["kind"].GetString());
        Assert.Equal(
            ["records"],
            entry.Details["outstanding"].EnumerateArray().Select(name => name.GetString()));
        Assert.Contains((Mona, Browser, StepUpAction.ErasureComplete), _stepUp.Asked);
        Assert.Equal(1, _work.Committed);
    }

    /// <summary>
    /// IDN-LIFE-003a: a restriction's failed delivery is closed by hand and recorded
    /// with its kind; no erasures row or ledger line is touched.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_AFailedRestrictionDeliveryIsCompletedByHandAsync()
    {
        _ledger = new ErasureLedgerInMemory();

        Delivery restriction = await FailedAsync(SubjectEventKind.RestrictionChanged);
        Delivery erasure = await ErasedAsync(Sara, Noon.AddHours(-1), ErasureStatus.Failed);

        Held(await CompletedAsync(Mona, new ErasureId(restriction.Id.Value)));

        PrivacyAuditEntry entry = Assert.Single(_audit.Entries);

        Assert.Equal(ErasureStatus.Complete, restriction.Status);
        Assert.Equal(ErasureStatus.Failed, erasure.Status);
        Assert.Equal(ErasureStatus.Failed, Assert.Single(_erasures.Erasures).Status);
        Assert.Empty(_ledger.Lines);
        Assert.Equal("restriction-changed", entry.Details["kind"].GetString());
        Assert.Equal(Ahmed, entry.Subject);
    }

    /// <summary>
    /// IDN-LIFE-003a: an export's delivery is none of the three kinds the manual path
    /// closes, so it is not found there, and an identifier naming nothing is not found
    /// either; the step-up is not asked for either (X8).
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_ADeliveryOfAnotherKindIsNotFoundAsync()
    {
        Delivery export = await FailedAsync(SubjectEventKind.ExportRequested);

        Assert.Equal(
            ErrorCodes.ErasureNotFound,
            Refused(await CompletedAsync(Mona, new ErasureId(export.Id.Value))).Code);
        Assert.Equal(
            ErrorCodes.ErasureNotFound,
            Refused(await CompletedAsync(Mona, new ErasureId(Guid.NewGuid()))).Code);
        Assert.Equal(ErasureStatus.Failed, export.Status);
        Assert.Empty(_audit.Entries);
        Assert.Empty(_stepUp.Asked);
    }

    /// <summary>
    /// 09 section 8a, chapter 10 section 5a: the manual completion requires a step-up
    /// of <c>erasure:complete</c> on the caller's own session, and a session that has
    /// not proved it closes nothing.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003a_TheManualCompletionRequiresStepUpAsync()
    {
        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);

        _stepUp.Closed = Error.From(ErrorCodes.StepUpRequired);

        Assert.Equal(
            ErrorCodes.StepUpRequired,
            Refused(await CompletedAsync(Mona, new ErasureId(delivery.Id.Value))).Code);
        Assert.Equal(ErasureStatus.Failed, delivery.Status);
        Assert.Equal(ErasureStatus.Failed, Assert.Single(_erasures.Erasures).Status);
        Assert.Empty(_audit.Entries);
    }

    /// <summary>
    /// 09 section 8a: every erasure operation is the <c>privacyrequest:manage</c>
    /// permission's, and a caller without it learns nothing and is not asked to step
    /// up.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task IDN_LIFE_003b_PrivacyRequestManageIsRequiredForEveryOperationAsync()
    {
        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);
        var erasure = new ErasureId(delivery.Id.Value);

        Assert.Equal(
            ErrorCodes.Denied,
            Refused(await Erasures.ListAsync(AccessContext.Of(Sara), TestContext.Current.CancellationToken)).Code);
        Assert.Equal(ErrorCodes.Denied, Refused(await ReadAsync(Sara, erasure)).Code);
        Assert.Equal(ErrorCodes.Denied, Refused(await CompletedAsync(Sara, erasure)).Code);
        Assert.Equal(ErasureStatus.Failed, delivery.Status);
        Assert.Empty(_stepUp.Asked);
    }

    /// <summary>
    /// DR-016 AC2: the ledger line is read with the rest of an erasure's progress, as
    /// the first required confirmation, where the deployment registered a ledger.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC2_AnErasureIsReadWithItsLedgerLineAsync()
    {
        _ledger = new ErasureLedgerInMemory();

        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);

        _outbox.Confirms(delivery, "erasure-ledger", Noon.AddMinutes(-50));

        ErasureProgress progress = Held(await ReadAsync(Mona, new ErasureId(delivery.Id.Value)));

        Assert.Equal(
            [
                new SubscriberConfirmation("erasure-ledger", Required: true, Noon.AddMinutes(-50)),
                new SubscriberConfirmation("records", Required: true, ConfirmedAt: null),
                new SubscriberConfirmation("newsletter", Required: false, ConfirmedAt: null),
            ],
            progress.Subscribers);
    }

    /// <summary>
    /// DR-016 AC2: the operator vouches for the host's subscribers and never for the
    /// ledger, so the manual path writes the line the attempts could not, then closes
    /// the erasure; what it records as outstanding is the host's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC2_AManualCompletionWritesTheLineBeforeItClosesTheErasureAsync()
    {
        _ledger = new ErasureLedgerInMemory();

        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);

        Held(await CompletedAsync(Mona, new ErasureId(delivery.Id.Value)));

        Assert.Equal(
            ["2026-09-24T11:00:00Z 11111111-1111-4111-8111-111111111111 minor-takedown"],
            _ledger.Lines);
        Assert.Contains("erasure-ledger", delivery.Confirmed);
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
        Assert.Equal(
            ["records"],
            Assert.Single(_audit.Entries).Details["outstanding"].EnumerateArray().Select(name => name.GetString()));
    }

    /// <summary>
    /// AUTHZ-GATE-006 AC3 and DR-016: a restriction of the operator committed after the
    /// gate step refuses the completion inside its unit of work, which rolls back with
    /// nothing closed and nothing recorded. The ledger line appended before the unit of
    /// work names an erasure committed before and stands; a completion made later closes
    /// the erasure, and the ledger holds that one line and no other.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task AUTHZ_GATE_006_AC3_ACompletionRefusedAtTheSecondAskLeavesTheLedgerLineStandingAsync()
    {
        const string line = "2026-09-24T11:00:00Z 11111111-1111-4111-8111-111111111111 minor-takedown";

        _ledger = new ErasureLedgerInMemory();

        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);
        var erasure = new ErasureId(delivery.Id.Value);

        _gate.Admitted = () => _work.Meanwhile = () => _gate.Restrict(Mona);

        Error refused = Refused(await CompletedAsync(Mona, erasure));

        Assert.Equal(ErrorCodes.Restricted, refused.Code);
        Assert.Equal([line], _ledger.Lines);
        Assert.Equal(ErasureStatus.Failed, delivery.Status);
        Assert.Equal(ErasureStatus.Failed, Assert.Single(_erasures.Erasures).Status);
        Assert.Empty(_audit.Entries);
        Assert.False(_work.Open);
        Assert.Equal(1, _work.Opened);
        Assert.Equal(0, _work.Committed);
        Assert.Equal(1, _work.RolledBack);

        _gate.Lift(Mona);

        Held(await CompletedAsync(Mona, erasure));

        Assert.Equal(ErasureStatus.Complete, delivery.Status);
        Assert.Equal([line], _ledger.Lines.Distinct());
    }

    /// <summary>
    /// DR-016 AC2: a line the worker already made durable is not written again by the
    /// manual path.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC2_ALineAlreadyWrittenIsNotWrittenAgainAsync()
    {
        _ledger = new ErasureLedgerInMemory();

        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);

        delivery.Confirm("erasure-ledger");

        Held(await CompletedAsync(Mona, new ErasureId(delivery.Id.Value)));

        Assert.Empty(_ledger.Lines);
        Assert.Equal(ErasureStatus.Complete, delivery.Status);
    }

    /// <summary>
    /// DR-016 AC2: while the ledger cannot take the line the manual path closes
    /// nothing and records nothing, and answers a fault, which is the environment's.
    /// </summary>
    /// <returns>The work of the test.</returns>
    [Fact]
    public async Task DR_016_AC2_AManualCompletionClosesNothingWhileTheLineCannotBeWrittenAsync()
    {
        _ledger = new ErasureLedgerInMemory { Durable = false };

        Delivery delivery = await ErasedAsync(Ahmed, Noon.AddHours(-1), ErasureStatus.Failed);

        Error refused = Refused(await CompletedAsync(Mona, new ErasureId(delivery.Id.Value)));

        Assert.Equal(ErrorCodes.SystemFault, refused.Code);
        Assert.Equal("erasure-ledger", refused.Details["handler"].GetString());
        Assert.Equal(ErasureStatus.Failed, delivery.Status);
        Assert.Equal(ErasureStatus.Failed, Assert.Single(_erasures.Erasures).Status);
        Assert.DoesNotContain("erasure-ledger", delivery.Confirmed);
        Assert.Empty(_audit.Entries);
        Assert.Equal(0, _work.Opened);
    }

    private static TValue Held<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            value => value,
            error => throw new InvalidOperationException($"The operation was refused with {error.Code}."));

    private static void Held(Result outcome) =>
        outcome.Switch(
            () => { },
            error => throw new InvalidOperationException($"The operation was refused with {error.Code}."));

    private static Error Refused<TValue>(Result<TValue> outcome) =>
        outcome.Match(
            _ => throw new InvalidOperationException("The operation succeeded."),
            error => error);

    private static Error Refused(Result outcome) =>
        outcome.Match(
            () => throw new InvalidOperationException("The operation succeeded."),
            error => error);

    // The erasure's delivery and its row, as the erasure's transaction wrote them and
    // the worker carried them since.
    private async ValueTask<Delivery> ErasedAsync(
        SubjectId subject,
        DateTimeOffset at,
        ErasureStatus status)
    {
        var delivery = Delivery.Of(subject, SubjectEventKind.ErasureRequested, at, reason: ErasureReason.MinorTakedown);
        var row = Erasure.Begun(subject, at, ErasureReason.MinorTakedown);

        delivery.Attempted(at, TimeSpan.FromSeconds(30), 2.0m, 1.0);
        row.RecordAttempt();

        if (status is ErasureStatus.Failed)
        {
            delivery.Fail();
            row.Fail();
        }
        else if (status is ErasureStatus.Complete)
        {
            delivery.Complete();
            row.Complete();
        }

        await _outbox.AddAsync(delivery, TestContext.Current.CancellationToken);
        _erasures.Add(row);

        return delivery;
    }

    // A delivery of another kind whose retries were spent.
    private async ValueTask<Delivery> FailedAsync(SubjectEventKind kind)
    {
        var delivery = Delivery.Of(Ahmed, kind, Noon.AddHours(-2));

        delivery.Attempted(Noon.AddHours(-2), TimeSpan.FromSeconds(30), 2.0m, 1.0);
        delivery.Fail();
        await _outbox.AddAsync(delivery, TestContext.Current.CancellationToken);

        return delivery;
    }

    private ValueTask<Result<ErasureProgress>> ReadAsync(SubjectId by, ErasureId erasure) =>
        Erasures.ReadAsync(AccessContext.Of(by), erasure, TestContext.Current.CancellationToken);

    private ValueTask<Result> CompletedAsync(SubjectId by, ErasureId erasure) =>
        Erasures.CompleteAsync(AccessContext.Of(by), Browser, erasure, TestContext.Current.CancellationToken);
}
