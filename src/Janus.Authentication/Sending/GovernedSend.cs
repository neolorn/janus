using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;

namespace Janus.Authentication.Sending;

/// <summary>
/// The one path every message an area sends takes into the library's outbox: judged
/// and counted at its admission in the caller's transaction, written there, and offered
/// one immediate attempt once that transaction has committed.
/// </summary>
/// <param name="admission">What judges and counts a send.</param>
/// <param name="outbox">Where an admitted message is written until it is carried.</param>
/// <param name="carrier">What attempts a message once its transaction has committed.</param>
/// <param name="signals">What is known about a number a restricted factor goes to.</param>
/// <param name="configuration">Where the declared languages and the retry delay come from.</param>
/// <param name="work">The caller's unit of work, which the attempt is registered on.</param>
/// <param name="time">The clock the deployment runs on.</param>
/// <param name="randomness">Where a correlation reference is drawn from.</param>
/// <remarks>
/// Implements AUTH-ABUSE-004, AUTH-ABUSE-002, AUTH-ABUSE-003, AUTH-FACT-002b,
/// IDN-ATTR-001, CONV-DESIGN-002, D-022 and INF-BG-001. It begins no unit of work: the
/// caller's is the one the counters are held in and the row is written in, so a refusal
/// leaves the caller free to go on and an operation that rolls back sends nothing.
/// </remarks>
internal sealed class GovernedSend(
    SendAdmission admission,
    ISendOutbox outbox,
    ISendCarrier carrier,
    PhoneSignals signals,
    IConfigurationStore configuration,
    IUnitOfWork work,
    TimeProvider time,
    RandomNumberGenerator randomness) : IGovernedSend, IFollowedSend, ISendingRestrictions
{
    // AUTH-ABUSE-004: what the one attempt after the commit took, of the messages this
    // operation undertook. A caller that follows a message reads it here: a row gone is
    // not by itself a send taken.
    private readonly HashSet<SendDeliveryId> _taken = [];

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The message is absent.</exception>
    public async ValueTask<Result<SendReference>> UndertakeAsync(
        OutboundMessage message,
        CancellationToken cancellationToken) =>
        (await AdmittedAsync(message, cancellationToken).ConfigureAwait(false))
            .Match(admitted => Result.Success(admitted[0].Reference), Result.Failure<SendReference>);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The message is absent.</exception>
    public async ValueTask<Result<IReadOnlyList<SendDeliveryId>>> AdmitAsync(
        OutboundMessage message,
        CancellationToken cancellationToken) =>
        (await AdmittedAsync(message, cancellationToken).ConfigureAwait(false))
            .Match(
                admitted => Result.Success<IReadOnlyList<SendDeliveryId>>([.. admitted.Select(one => one.Id)]),
                Result.Failure<IReadOnlyList<SendDeliveryId>>);

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The messages are absent.</exception>
    public ValueTask<bool> CarriedAsync(
        IReadOnlyList<SendDeliveryId> admitted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admitted);

        return ValueTask.FromResult(admitted.All(_taken.Contains));
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">The message is absent.</exception>
    /// <remarks>
    /// AUTH-ABUSE-002 AC3: the send is judged exactly as <see cref="UndertakeAsync"/>
    /// judges the message it stands for, the gateway floor included, and counted once
    /// for each message it would have been, each under a reference no delivery report
    /// will ever name, so what it counts is kept until its buckets are empty.
    /// </remarks>
    public async ValueTask<Result> DrawAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        IReadOnlyList<string?> owed = (await OwedAsync(message, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string?>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        IReadOnlyList<SendPlan> plans = (await admission
                .JudgeAsync(message, owed.Count, setAside: null, now, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<SendPlan>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure(failure);
        }

        foreach (SendPlan plan in plans)
        {
            await admission
                .CountAsync(SendReference.Draw(randomness), plan, now, cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }

    // One message undertaken inside the unit of work in progress: what was written to
    // the outbox for it, one row, or one for each declared language of a text message
    // owed in every one of them.
    private async ValueTask<Result<IReadOnlyList<SendDelivery>>> AdmittedAsync(
        OutboundMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        DateTimeOffset now = time.GetUtcNow();
        Error? failure = null;

        IReadOnlyList<string?> owed = (await OwedAsync(message, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<string?>>(error, ref failure));

        TimeSpan initial = (await configuration
                .ReadAsync(Settings.OutboxRetryInitial, cancellationToken).ConfigureAwait(false))
            .Match(value => value, error => Held<TimeSpan>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<SendDelivery>>(failure);
        }

        IReadOnlyList<SendPlan> plans = (await admission
                .JudgeAsync(message, owed.Count, setAside: null, now, cancellationToken)
                .ConfigureAwait(false))
            .Match(value => value, error => Held<IReadOnlyList<SendPlan>>(error, ref failure));

        if (failure is not null)
        {
            return Result.Failure<IReadOnlyList<SendDelivery>>(failure);
        }

        // AUTH-FACT-002b: the restricted entries ride a number, so what the deployment
        // knows about the number is considered here, once the restrictions have let the
        // send through and before it is written.
        await signals.ConsiderAsync(message, cancellationToken).ConfigureAwait(false);

        // AUTH-ABUSE-003: an ask of a sign-in link, an email code or a recovery is
        // answered before any transport is called, so its message is left to the
        // publisher and is due at once. Every other message has one attempt after the
        // commit: its row is written due the first retry delay after its admission, with
        // no jitter, so that no pass takes it before that attempt has had its chance
        // (CONV-DESIGN-003, AUTH-ABUSE-004, D-188).
        bool attempted = !MessageChannels.AnsweredFirst.Contains(message.Message);
        var admitted = new List<SendDelivery>(owed.Count);

        for (int index = 0; index < owed.Count; index++)
        {
            var reference = SendReference.Draw(randomness);

            var delivery = SendDelivery.Of(
                message with { Language = owed[index] },
                reference,
                now,
                attempted ? initial : TimeSpan.Zero);

            await outbox.AddAsync(delivery, cancellationToken).ConfigureAwait(false);
            await admission.CountAsync(reference, plans[index], now, cancellationToken).ConfigureAwait(false);

            if (attempted)
            {
                SendDeliveryId written = delivery.Id;

                work.AfterCommit(token => AttemptedAsync(written, token))
                    .Switch(() => { }, error => throw new InvalidOperationException(error.Code.ToString()));
            }

            admitted.Add(delivery);
        }

        return Result.Success<IReadOnlyList<SendDelivery>>(admitted);
    }

    private async ValueTask AttemptedAsync(SendDeliveryId delivery, CancellationToken cancellationToken)
    {
        if (await carrier.AttemptAsync(delivery, cancellationToken).ConfigureAwait(false))
        {
            _ = _taken.Add(delivery);
        }
    }

    private static TValue Held<TValue>(Error error, ref Error? failure)
    {
        failure = error;

        return default!;
    }

    // IDN-ATTR-001, AUTH-ABUSE-004: a message whose recipient's language is known is one
    // message in it. Where none is known, a mail is one message carrying every declared
    // language, and a text message is one message for each of them.
    private async ValueTask<Result<IReadOnlyList<string?>>> OwedAsync(
        OutboundMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Language is not null || message.Kind is SendKind.Email)
        {
            return Result.Success<IReadOnlyList<string?>>([message.Language]);
        }

        return (await configuration
                .ReadAsync(Settings.NotificationLanguages, cancellationToken)
                .ConfigureAwait(false))
            .Match(
                declared => declared.Count == 0
                    ? Result.Failure<IReadOnlyList<string?>>(Undeclared())
                    : Result.Success<IReadOnlyList<string?>>([.. declared]),
                Result.Failure<IReadOnlyList<string?>>);
    }

    private static Error Undeclared() =>
        Error.From(
            ErrorCodes.StartupDeclarationMissing,
            "key",
            JsonSerializer.SerializeToElement(Settings.NotificationLanguages.Key.ToString()));
}
