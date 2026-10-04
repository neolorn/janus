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
/// <param name="configuration">Where the retry schedule is read.</param>
/// <param name="alerts">Where the alert of a push marked failed goes.</param>
/// <param name="work">The one transaction each mailbox's progress is recorded in.</param>
/// <param name="time">The clock the schedule is computed against.</param>
/// <param name="randomness">Where the full jitter of each delay comes from.</param>
/// <remarks>
/// Implements INT-MAIL-001 AC4, INT-MAIL-006, INT-MAIL-006a AC1, INT-MAIL-007 AC1, AC3,
/// AC5 to AC8, OPS-OBS-002, D-177 and D-178. A suspension or a membership end commits
/// wherever it happens, and the first pass after it pushes the disabled state. Each
/// attempt is recorded, and committed, before it is made. A push that creates or
/// changes a mailbox waits, spending nothing, while another mailbox at its address is
/// owed a removal the server has not confirmed, as the one a <c>replace</c> left is. A push that spends its budget,
/// or that the server answers with a conflict, is marked failed and raises
/// <c>degradation</c>; it is begun again under its key a day later, for as long as its
/// state is owed, and raises its alert again if that run fails too. What is resumed is
/// the library's own undelivered change: nothing corrects the server behind the
/// operator's back, and reconciliation goes on reporting any difference.
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
            Mailbox mailbox = standing.Mailbox;

            if (mailbox.IsSettled(standing.Stands))
            {
                continue;
            }

            if (schedule is null)
            {
                Error? failure = null;

                schedule = (await ScheduleAsync(cancellationToken).ConfigureAwait(false))
                    .Match(read => read, error => Withheld<Schedule>(error, ref failure));

                if (failure is not null)
                {
                    return Result.Failure<int>(failure);
                }
            }

            Guid? outstanding = mailbox.PendingKey;
            MailboxPush? push = mailbox.Due(standing.Stands, now);

            // INT-MAIL-007 AC5, D-177: the account the old mailbox holds at the address
            // would meet the new one's push, so that push waits for the removal; a
            // removal never waits, so two at one address cannot hold each other.
            if (push is null
                || (push.State is not MailboxState.Removed && removing.Contains(mailbox.Address.Value)))
            {
                // A change of the state owed is written down under its new key even
                // when its first attempt is not yet due.
                if (mailbox.PendingKey != outstanding
                    && await RecordedAsync(mailbox, alert: null, cancellationToken).ConfigureAwait(false)
                        is Error unrecorded)
                {
                    return Result.Failure<int>(unrecorded);
                }

                continue;
            }

            // INT-MAIL-007 AC7, D-177: the server holds nothing of a mailbox no push of
            // which was ever attempted, so its removal is confirmed without being sent.
            if (mailbox.IsUnsent)
            {
                mailbox.Confirmed();

                if (await RecordedAsync(mailbox, alert: null, cancellationToken).ConfigureAwait(false)
                    is Error unconfirmed)
                {
                    return Result.Failure<int>(unconfirmed);
                }

                confirmed++;

                continue;
            }

            // INT-MAIL-007 AC1: a push is written down under its key, and each attempt
            // counted, before it leaves, so one the server applies while the process
            // stops is still outstanding, under the same key and with the attempt
            // spent, when the process returns.
            mailbox.Attempting();

            if (await RecordedAsync(mailbox, alert: null, cancellationToken).ConfigureAwait(false)
                is Error unattempted)
            {
                return Result.Failure<int>(unattempted);
            }

            Error? refused = (await ProvisionedAsync(server, push, cancellationToken).ConfigureAwait(false))
                .Match<Error?>(() => null, error => error);
            AlertRaised? alert = null;

            if (refused is null)
            {
                mailbox.Confirmed();
                confirmed++;
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

            if (await RecordedAsync(mailbox, alert, cancellationToken).ConfigureAwait(false) is Error unanswered)
            {
                return Result.Failure<int>(unanswered);
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

    // A server that throws is a server that did not confirm. Letting the fault out
    // would leave the attempt uncounted, so the push would be made at every pass,
    // never back off and never spend its budget.
    private static async ValueTask<Result> ProvisionedAsync(
        IMailServer server,
        MailboxPush push,
        CancellationToken cancellationToken)
    {
        try
        {
            return await server.ProvisionAsync(push, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            return Result.Failure(Error.From(ErrorCodes.SystemFault));
        }
    }

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

    // Full jitter: the delay is a uniform fraction of the computed backoff, so two
    // pushes failing together do not retry together.
    private double Jitter()
    {
        Span<byte> bytes = stackalloc byte[2];

        randomness.GetBytes(bytes);

        return BinaryPrimitives.ReadUInt16LittleEndian(bytes) / (double)ushort.MaxValue;
    }

    // The mailbox's progress is written in one transaction, with the alert where one is
    // raised, so a push marked failed is recorded with its alert or not at all.
    private async ValueTask<Error?> RecordedAsync(
        Mailbox mailbox,
        AlertRaised? alert,
        CancellationToken cancellationToken)
    {
        if ((await work.BeginAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error notBegun)
        {
            return notBegun;
        }

        await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);

        if (alert is not null
            && (await alerts.RaiseAsync(alert, cancellationToken).ConfigureAwait(false))
                .Match(() => (Error?)null, error => error) is Error unalerted)
        {
            await work.RollbackAsync().ConfigureAwait(false);

            return unalerted;
        }

        return (await work.CommitAsync(cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error);
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

        return failure is null
            ? Result.Success(new Schedule(initial, factor, attempts))
            : Result.Failure<Schedule>(failure);
    }

    private sealed record Schedule(TimeSpan Initial, decimal Factor, int MaxAttempts);
}
