using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Mailboxes;

/// <summary>
/// One pass of the outbox publisher over the mailboxes: each one whose server state
/// is not the state it is owed is pushed that state, under a key that stays the same
/// until the server confirms it.
/// </summary>
/// <param name="mailboxes">Where the mailboxes are.</param>
/// <param name="inUse">The mail server in use, where the deployment has one.</param>
/// <param name="configuration">Where the retry schedule and the claim's timeout are read.</param>
/// <param name="alerts">Where the alert of a push marked failed goes.</param>
/// <param name="work">The transactions a claim, an attempt and an outcome are each written in.</param>
/// <param name="time">The clock the schedule is computed against.</param>
/// <param name="randomness">Where the full jitter of each delay comes from.</param>
/// <remarks>
/// Implements INT-MAIL-001 AC4, INT-MAIL-006, INT-MAIL-006a AC1, INT-MAIL-007 AC1, AC3,
/// AC5 to AC8, OPS-OBS-002, CONV-DESIGN-002, CONV-DESIGN-003, INF-BG-001, D-177 and
/// D-178. A suspension or a membership end commits wherever it happens, and the first
/// pass after it pushes the disabled state. A pass reads the mailboxes without a lock
/// and claims each one it has something to do for, by one conditional update committed
/// on its own, before it decides on the row as it then stands; the attempt and its
/// outcome are each written by one update conditional on that claim, so passes in any
/// number of processes make each push once at a time, count each attempt once and
/// record each outcome once. Each attempt is recorded, and committed, before it is
/// made, and the mail server is never called while a transaction is open. A push that
/// creates or changes a mailbox waits, spending nothing, while another mailbox at its
/// address is owed a removal the server has not confirmed, as the one a <c>replace</c>
/// left is. A push that spends its budget, or that the server answers with a conflict,
/// is marked failed and raises <c>degradation</c>; it is begun again under its key a day
/// later, for as long as its state is owed, and raises its alert again if that run fails
/// too. What is resumed is the library's own undelivered change: nothing corrects the
/// server behind the operator's back, and reconciliation goes on reporting any
/// difference.
/// </remarks>
internal sealed class MailboxPublisher(
    IMailboxStore mailboxes,
    IMailServerInUse inUse,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="context">The system principal the pass runs as.</param>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many pushes the server confirmed, or the failure that stopped the pass.</returns>
    /// <exception cref="ArgumentException">The context is not a principal that may deliver what has been committed.</exception>
    public async ValueTask<Result<int>> PublishAsync(AccessContext context, CancellationToken cancellationToken)
    {
        _ = Delivering(context);

        if (inUse.Chosen().Match<IMailServer?>(chosen => chosen, _ => null) is not IMailServer server)
        {
            return Result.Success(0);
        }

        DateTimeOffset now = time.GetUtcNow();
        IReadOnlyList<MailboxStanding> held = await mailboxes.AllAsync(cancellationToken)
            .ConfigureAwait(false);

        Schedule? schedule = null;
        int confirmed = 0;
        HashSet<string> removing = Removing(held);

        foreach (MailboxStanding standing in held)
        {
            // CONV-DESIGN-003: the rows were read without a lock, and one a pass has
            // nothing to do for is not claimed.
            if (!standing.Mailbox.Awaits(standing.Stands, now, Waits(standing, removing)))
            {
                continue;
            }

            Error? failure = null;

            if (schedule is null)
            {
                schedule = (await ScheduleAsync(cancellationToken).ConfigureAwait(false))
                    .Match(read => read, error => Withheld<Schedule>(error, ref failure));

                if (failure is not null)
                {
                    return Result.Failure<int>(failure);
                }
            }

            bool pushed = (await CarriedAsync(server, standing.Mailbox.Id, removing, schedule, cancellationToken)
                    .ConfigureAwait(false))
                .Match(value => value, error => Withheld<bool>(error, ref failure));

            if (failure is not null)
            {
                return Result.Failure<int>(failure);
            }

            if (pushed)
            {
                confirmed++;
            }
        }

        return Result.Success(confirmed);
    }

    // INF-BG-002 AC1, IDN-PRIN-001 AC3 (D-166, 304): the pass runs as a named
    // principal that may deliver what has been committed, and never as nobody.
    private static SystemPrincipal Delivering(AccessContext context) =>
        context?.Principal is { } principal && principal.MayRun(SystemOperation.Delivery)
            ? principal
            : throw new ArgumentException(
                "The pass runs as a system principal that may deliver what has been committed.",
                nameof(context));

    // The canonical addresses at which a mailbox is owed a removal the server has not
    // confirmed, as the pass reads them. A mailbox whose holder was erased is not read,
    // so a push that waited for it waits no longer (D-178).
    private static HashSet<string> Removing(IReadOnlyList<MailboxStanding> held)
    {
        var removing = new HashSet<string>(StringComparer.Ordinal);

        foreach (MailboxStanding standing in held)
        {
            if (standing.Mailbox.Owed(standing.Stands) is MailboxState.Removed
                && standing.Mailbox.Pushed is not MailboxState.Removed)
            {
                _ = removing.Add(standing.Mailbox.Address.Value);
            }
        }

        return removing;
    }

    // INT-MAIL-007 AC5, D-177: the account the old mailbox holds at the address would
    // meet the new one's push, so that push waits for the removal; a removal never
    // waits, so two at one address cannot hold each other.
    private static bool Waits(MailboxStanding standing, HashSet<string> removing) =>
        standing.Mailbox.Owed(standing.Stands) is not MailboxState.Removed
        && removing.Contains(standing.Mailbox.Address.Value);

    // The mailbox is named by its identifier: the address is personal data and an
    // alert travels to channels that are not the account's.
    private static Dictionary<string, JsonElement> Exhausted(Mailbox mailbox) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["mailbox"] = JsonSerializer.SerializeToElement(mailbox.Id.ToString()),
            ["state"] = JsonSerializer.SerializeToElement(WrittenName.Of(mailbox.Pending!.Value)),
            ["attempts"] = JsonSerializer.SerializeToElement(mailbox.Attempts),
        };

    // D-177: the conflict's alert names the mailbox and the state its push carried.
    private static Dictionary<string, JsonElement> Conflicting(Mailbox mailbox) =>
        new(capacity: 2, StringComparer.Ordinal)
        {
            ["mailbox"] = JsonSerializer.SerializeToElement(mailbox.Id.ToString()),
            ["state"] = JsonSerializer.SerializeToElement(WrittenName.Of(mailbox.Pending!.Value)),
        };

    private static TValue Withheld<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // One mailbox's turn in a pass: claimed, decided on as its row then stands, its
    // attempt counted and its push made outside any transaction, and what came of it
    // written under the claim. It answers whether the server now holds the state owed.
    private async ValueTask<Result<bool>> CarriedAsync(
        IMailServer server,
        MailboxId id,
        HashSet<string> removing,
        Schedule schedule,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        // CONV-DESIGN-003: the claim is one conditional update committed on its own,
        // before the mail server is called; a row another pass holds is that pass's.
        DateTimeOffset? claimed = (await InUnitAsync(
                async () => Result.Success(await mailboxes
                    .ClaimAsync(id, now, schedule.ClaimTimeout, cancellationToken)
                    .ConfigureAwait(false)),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(value => value, error => Withheld<DateTimeOffset?>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<bool>(failure);
        }

        if (claimed is not DateTimeOffset claim)
        {
            return Result.Success(false);
        }

        // What the pass read before the claim may have been written since, by another
        // pass or by the operation that changed the state owed, so the row is read
        // again and decided on as it stands under the claim.
        if (await mailboxes.StandingAsync(id, cancellationToken).ConfigureAwait(false)
            is not MailboxStanding standing)
        {
            return await ReleasedAsync(id, claim, cancellationToken).ConfigureAwait(false);
        }

        Mailbox mailbox = standing.Mailbox;
        Guid? outstanding = mailbox.PendingKey;
        MailboxPush? push = mailbox.Due(standing.Stands, now);

        if (push is null || Waits(standing, removing))
        {
            // A change of the state owed is written down under its new key even when
            // its first attempt is not yet due.
            return mailbox.PendingKey == outstanding
                ? await ReleasedAsync(id, claim, cancellationToken).ConfigureAwait(false)
                : (await RecordedAsync(mailbox, claim, alert: null, cancellationToken).ConfigureAwait(false))
                    .Match(_ => Result.Success(false), Result.Failure<bool>);
        }

        // INT-MAIL-007 AC7, D-177: the server holds nothing of a mailbox no push of
        // which was ever attempted, so its removal is confirmed without being sent.
        if (mailbox.IsUnsent)
        {
            mailbox.Confirmed();

            return await RecordedAsync(mailbox, claim, alert: null, cancellationToken).ConfigureAwait(false);
        }

        // INT-MAIL-007 AC1: a push is written down under its key, and each attempt
        // counted, before it leaves, so one the server applies while the process
        // stops is still outstanding, under the same key and with the attempt
        // spent, when the process returns.
        mailbox.Attempting();

        bool attempting = (await InUnitAsync(
                async () => Result.Success(await mailboxes
                    .AttemptAsync(mailbox, claim, cancellationToken)
                    .ConfigureAwait(false)),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(value => value, error => Withheld<bool>(error, ref failure));

        if (failure is not null || !attempting)
        {
            return failure is null ? Result.Success(false) : Result.Failure<bool>(failure);
        }

        Error? refused = (await ProvisionedAsync(server, push, schedule.ClaimTimeout, cancellationToken)
                .ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
        AlertRaised? alert = null;

        if (refused is null)
        {
            mailbox.Confirmed();
        }
        else if (refused.Code == ErrorCodes.MailServerConflict)
        {
            // INT-MAIL-001 AC4, D-177: retrying within the run cannot resolve an
            // account someone must resolve at the mail server, so the push is marked
            // failed at this attempt and raises its own alert.
            mailbox.Failed(now);
            alert = Alerts.Scoped(AlertCondition.Degradation, "mailbox.conflict:" + mailbox.Id, now, Conflicting(mailbox));
        }
        else if (mailbox.Refused(now, schedule.Initial, schedule.Factor, schedule.MaxAttempts, Jitter()))
        {
            // INT-MAIL-007 AC3: a push the server never took is visible the moment
            // its budget is spent.
            alert = Alerts.Scoped(AlertCondition.Degradation, "mailbox.push:" + mailbox.Id, now, Exhausted(mailbox));
        }

        return (await RecordedAsync(mailbox, claim, alert, cancellationToken).ConfigureAwait(false))
            .Match(written => Result.Success(written && refused is null), Result.Failure<bool>);
    }

    // The server is asked outside any transaction, for no longer than the claim
    // stands: an attempt still running then is abandoned as a failed attempt. A server
    // that throws is a server that did not confirm. Letting the fault out would leave
    // the push without its next attempt, so it would be made at every pass and never
    // back off.
    private async ValueTask<Result> ProvisionedAsync(
        IMailServer server,
        MailboxPush push,
        TimeSpan claimTimeout,
        CancellationToken cancellationToken)
    {
        using var abandoned = new CancellationTokenSource(claimTimeout, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(abandoned.Token, cancellationToken);

        try
        {
            return await server.ProvisionAsync(push, either.Token).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }
    }

    // What a pass made of a mailbox's push, written under the claim it was made under,
    // with the alert where one is raised, so a push marked failed is recorded with its
    // alert or not at all. An outcome whose claim was taken over changes nothing and
    // raises nothing. It answers whether the outcome was written.
    private async ValueTask<Result<bool>> RecordedAsync(
        Mailbox mailbox,
        DateTimeOffset claim,
        AlertRaised? alert,
        CancellationToken cancellationToken) =>
        await InUnitAsync(
                async () =>
                {
                    if (!await mailboxes.RecordAsync(mailbox, claim, cancellationToken).ConfigureAwait(false))
                    {
                        return Result.Success(false);
                    }

                    return alert is null
                        ? Result.Success(true)
                        : (await alerts.RaiseAsync(alert, cancellationToken).ConfigureAwait(false))
                            .Match(() => Result.Success(true), Result.Failure<bool>);
                },
                cancellationToken)
            .ConfigureAwait(false);

    // A row the pass has nothing to write for gives its claim up, so the next pass
    // need not wait for it to time out. Nothing was pushed.
    private async ValueTask<Result<bool>> ReleasedAsync(
        MailboxId id,
        DateTimeOffset claim,
        CancellationToken cancellationToken) =>
        (await InUnitAsync(
                async () => Result.Success(await mailboxes
                    .ReleaseAsync(id, claim, cancellationToken)
                    .ConfigureAwait(false)),
                cancellationToken)
            .ConfigureAwait(false))
            .Match(_ => Result.Success(false), Result.Failure<bool>);

    // One unit of work of the pass's own: committed where the work succeeds and rolled
    // back on every other return, a fault included (CONV-DESIGN-003).
    private async ValueTask<Result<TValue>> InUnitAsync<TValue>(
        Func<ValueTask<Result<TValue>>> written,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return Result.Failure<TValue>(notBegun);
        }

        Result<TValue> outcome = await FaultRolledBackAsync(written, cancellationToken).ConfigureAwait(false);

        if (outcome.Match(_ => (Error?)null, error => error) is not null)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return outcome;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match(() => outcome, Result.Failure<TValue>);
    }

    private async ValueTask<Result<TValue>> FaultRolledBackAsync<TValue>(
        Func<ValueTask<Result<TValue>>> written,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await written().ConfigureAwait(false);
        }
        catch
        {
            await work.RollbackAsync().ConfigureAwait(false);

            throw;
        }
    }

    // Full jitter: the delay is a uniform fraction of the computed backoff, so two
    // pushes failing together do not retry together.
    private double Jitter()
    {
        Span<byte> bytes = stackalloc byte[2];

        randomness.GetBytes(bytes);

        return BinaryPrimitives.ReadUInt16LittleEndian(bytes) / (double)ushort.MaxValue;
    }

    private async ValueTask<Result<Schedule>> ScheduleAsync(CancellationToken cancellationToken)
    {
        Error? failure = null;

        TimeSpan initial = (await configuration
                .ReadAsync(Settings.OutboxRetryInitial, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        decimal factor = (await configuration
                .ReadAsync(Settings.OutboxRetryFactor, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<decimal>(error, ref failure));

        int attempts = (await configuration
                .ReadAsync(Settings.OutboxRetryMaxAttempts, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<int>(error, ref failure));

        TimeSpan claim = (await configuration
                .ReadAsync(Settings.OutboxClaimTimeout, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Withheld<TimeSpan>(error, ref failure));

        return failure is null
            ? Result.Success(new Schedule(initial, factor, attempts, claim))
            : Result.Failure<Schedule>(failure);
    }

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts, TimeSpan ClaimTimeout);
}
