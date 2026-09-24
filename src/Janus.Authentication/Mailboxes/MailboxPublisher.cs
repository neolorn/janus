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
/// <param name="server">The mail server, absent where the deployment registered none.</param>
/// <param name="configuration">Where the retry schedule is read.</param>
/// <param name="alerts">Where a spent budget's alert goes.</param>
/// <param name="work">The one transaction each mailbox's progress is recorded in.</param>
/// <param name="time">The clock the schedule is computed against.</param>
/// <param name="randomness">Where the full jitter of each delay comes from.</param>
/// <remarks>
/// Implements INT-MAIL-006, INT-MAIL-006a AC1, INT-MAIL-007 AC1 and AC3, and
/// OPS-OBS-002. A suspension or a membership end commits wherever it happens, and the
/// first pass after it pushes the disabled state. A push that spends its budget raises
/// <c>degradation</c> and stays failed until the state owed changes again: nothing
/// corrects the server behind the operator's back, and reconciliation goes on
/// reporting the difference.
/// </remarks>
internal sealed class MailboxPublisher(
    IMailboxStore mailboxes,
    IMailServer? server,
    IConfigurationStore configuration,
    IAlertChannels alerts,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness)
{
    /// <summary>
    /// Runs one pass.
    /// </summary>
    /// <param name="cancellationToken">Abandons the pass.</param>
    /// <returns>How many pushes the server confirmed, or the failure that stopped the pass.</returns>
    public async ValueTask<Result<int>> PublishAsync(CancellationToken cancellationToken)
    {
        if (server is null)
        {
            return Result.Success(0);
        }

        DateTimeOffset now = time.GetUtcNow();
        IReadOnlyList<MailboxStanding> held = await mailboxes.AllAsync(cancellationToken)
            .ConfigureAwait(false);

        Schedule? schedule = null;
        int confirmed = 0;

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

            // INT-MAIL-007 AC1: a push is written down under its key before it leaves,
            // so one the server applies while the process stops is still outstanding,
            // under the same key, when the process returns.
            if (mailbox.PendingKey != outstanding)
            {
                await work.BeginAsync(cancellationToken).ConfigureAwait(false);
                await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);
                await work.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (push is null)
            {
                continue;
            }

            bool spent = false;

            if ((await ProvisionedAsync(server, push, cancellationToken).ConfigureAwait(false))
                .Match(() => true, _ => false))
            {
                mailbox.Confirmed();
                confirmed++;
            }
            else
            {
                spent = mailbox.Refused(
                    now,
                    schedule.Initial,
                    schedule.Factor,
                    schedule.MaxAttempts,
                    Jitter());
            }

            await work.BeginAsync(cancellationToken).ConfigureAwait(false);
            await mailboxes.RecordAsync(mailbox, cancellationToken).ConfigureAwait(false);

            // INT-MAIL-007 AC3: a push the server never took is visible the moment
            // its budget is spent, and is recorded with the alert or not at all.
            if (spent
                && (await alerts
                        .RaiseAsync(
                            Alerts.Of(AlertCondition.Degradation, Scope(mailbox), now, Exhausted(mailbox)),
                            cancellationToken)
                        .ConfigureAwait(false))
                    .Match(() => (Error?)null, error => error) is Error unalerted)
            {
                return Result.Failure<int>(unalerted);
            }

            await work.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(confirmed);
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

    private static string Scope(Mailbox mailbox) => "mailbox.push:" + mailbox.Id;

    // The mailbox is named by its identifier: the address is personal data and an
    // alert travels to channels that are not the account's.
    private static Dictionary<string, JsonElement> Exhausted(Mailbox mailbox) =>
        new(capacity: 3, StringComparer.Ordinal)
        {
            ["mailbox"] = JsonSerializer.SerializeToElement(mailbox.Id.ToString()),
            ["state"] = JsonSerializer.SerializeToElement(WrittenName.Of(mailbox.Pending!.Value)),
            ["attempts"] = JsonSerializer.SerializeToElement(mailbox.Attempts),
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
