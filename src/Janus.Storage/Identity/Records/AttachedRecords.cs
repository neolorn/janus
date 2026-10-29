using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Janus.Core.Configuration;
using Janus.Identity.Records;
using Janus.Privacy.Erasures;
using Janus.Privacy.Outbox;

namespace Janus.Storage.Identity.Records;

/// <summary>
/// Each transition of an account into or out of restricted, deleting and deleted,
/// carried to the person records attached to it.
/// </summary>
/// <param name="records">Where each attached record is held and its transition carried.</param>
/// <param name="outbox">Where a record's restriction, lift and takedown are announced to the subscribers.</param>
/// <param name="events">Where a record's deletion window, its cancellation and a takedown's reversal are announced.</param>
/// <param name="configuration">Where the length of the account's deletion window is read.</param>
/// <remarks>
/// Implements REC-ATT-002. Each runs inside the transaction of the account's own
/// transition, with each record's row held under its lock, so the account and its records
/// commit together. A record whose state the transition does not leave from is left as
/// it is: each holds what it held, as for the record alone.
/// </remarks>
internal sealed class AttachedRecords(
    IPersonRecordStore records,
    IOutboxStore outbox,
    IEvents events,
    IConfigurationStore configuration)
{
    /// <summary>
    /// Restricts each record attached to the account; one in its deletion window holds
    /// the restriction, as the account does.
    /// </summary>
    /// <param name="account">The account restricted.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of restricting them.</returns>
    public ValueTask RestrictAsync(SubjectId account, DateTimeOffset at, CancellationToken cancellationToken) =>
        EachAsync(
            account,
            async (record, token) =>
            {
                switch (record)
                {
                    case { State: PersonRecordState.Active }:
                        record.Restrict();

                        break;

                    case { State: PersonRecordState.Deleting, RestrictionHeld: false }:
                        record.HoldRestriction();

                        break;

                    default:
                        return false;
                }

                await outbox
                    .AddAsync(Delivery.Of(record.Subject, SubjectEventKind.RestrictionChanged, at, restricted: true), token)
                    .ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Lifts the restriction of each restricted record attached to the account.
    /// </summary>
    /// <param name="account">The account whose restriction was lifted.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of lifting them.</returns>
    public ValueTask LiftRestrictionAsync(SubjectId account, DateTimeOffset at, CancellationToken cancellationToken) =>
        EachAsync(
            account,
            async (record, token) =>
            {
                if (record.State is not PersonRecordState.Restricted)
                {
                    return false;
                }

                record.LiftRestriction();

                await outbox
                    .AddAsync(Delivery.Of(record.Subject, SubjectEventKind.RestrictionChanged, at, restricted: false), token)
                    .ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Begins the deletion window of each active or restricted record attached to the
    /// account, with the account's origin and window.
    /// </summary>
    /// <param name="account">The account whose window began.</param>
    /// <param name="origin">The person, or a request that arrived out of band.</param>
    /// <param name="at">When the window began.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of beginning them.</returns>
    public async ValueTask BeginDeletionAsync(
        SubjectId account,
        DeletionOrigin origin,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        DateTimeOffset erasesAt = (await DeletionWindows.ReadAsync(configuration, cancellationToken).ConfigureAwait(false))
            .ErasureDue(origin, at, heldSince: null);

        await EachAsync(
                account,
                async (record, token) =>
                {
                    if (record.State is not (PersonRecordState.Active or PersonRecordState.Restricted))
                    {
                        return false;
                    }

                    record.RequestDeletion(origin, at);

                    await PublishedAsync(
                            new RecordDeletionRequested(at, Key(record.Subject, at), origin, erasesAt) { Subject = record.Subject },
                            token)
                        .ConfigureAwait(false);

                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels the deletion window of each record attached to the account that is in one
    /// a takedown did not begin.
    /// </summary>
    /// <param name="account">The account whose window was cancelled.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of cancelling them.</returns>
    public ValueTask CancelDeletionAsync(SubjectId account, DateTimeOffset at, CancellationToken cancellationToken) =>
        EachAsync(
            account,
            async (record, token) =>
            {
                if (record is not { State: PersonRecordState.Deleting, DeletingBy: not DeletionOrigin.Takedown })
                {
                    return false;
                }

                record.CancelDeletion();

                await PublishedAsync(new RecordDeletionCancelled(at, Key(record.Subject, at)) { Subject = record.Subject }, token)
                    .ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Takes down each record attached to the account, holding what it held.
    /// </summary>
    /// <param name="account">The account taken down.</param>
    /// <param name="at">When the takedown was triggered.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking them down.</returns>
    public ValueTask TakeDownAsync(SubjectId account, DateTimeOffset at, CancellationToken cancellationToken) =>
        EachAsync(
            account,
            async (record, token) =>
            {
                if (record is not ({ State: PersonRecordState.Active or PersonRecordState.Restricted }
                    or { State: PersonRecordState.Deleting, DeletingBy: not DeletionOrigin.Takedown }))
                {
                    return false;
                }

                record.Takedown(at);

                await outbox
                    .AddAsync(Delivery.Of(record.Subject, SubjectEventKind.TakedownExecuted, at), token)
                    .ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Returns each record attached to the account that a takedown holds to what it held.
    /// </summary>
    /// <param name="account">The account whose takedown was reversed.</param>
    /// <param name="at">When.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of reversing them.</returns>
    public ValueTask ReverseTakedownAsync(SubjectId account, DateTimeOffset at, CancellationToken cancellationToken) =>
        EachAsync(
            account,
            async (record, token) =>
            {
                if (record is not { State: PersonRecordState.Deleting, DeletingBy: DeletionOrigin.Takedown })
                {
                    return false;
                }

                record.ReverseTakedown();

                await PublishedAsync(new TakedownReversed(at, Key(record.Subject, at)) { Subject = record.Subject }, token)
                    .ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    // REC-ATT-002: the key a consumer recognises a repeat by is the record's, as for the
    // record alone.
    private static string Key(SubjectId record, DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"{record.Value}@{at.UtcTicks}");

    // An event not taken fails the account's transition, as its unit of work failing does.
    private async ValueTask PublishedAsync<TEvent>(TEvent raised, CancellationToken cancellationToken)
        where TEvent : DomainEvent
    {
        if ((await events.PublishAsync(raised, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error unpublished)
        {
            throw new InvalidOperationException(unpublished.Code.ToString());
        }
    }

    private async ValueTask EachAsync(
        SubjectId account,
        Func<PersonRecord, CancellationToken, ValueTask<bool>> transition,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectId> attached = await records.AttachedToAsync(account, cancellationToken)
            .ConfigureAwait(false);

        foreach (SubjectId subject in attached)
        {
            if (await records.HoldAsync(subject, cancellationToken).ConfigureAwait(false) is PersonRecord record
                && await transition(record, cancellationToken).ConfigureAwait(false))
            {
                await records.RecordAsync(record, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
