using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Events;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Events;

/// <summary>
/// Where the emitted events wait for their consumers.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <remarks>
/// Implements LIB-API-001, CONV-DESIGN-002 and CONV-DESIGN-003. The kind a row carries
/// is the event's name in chapter 10 section 5b, and the payload is the event itself,
/// so a retry days later offers what the transaction raised. A row is claimed whole by
/// one conditional update, and a renewal, a take and the outcome are each one update
/// conditional on that claim.
/// </remarks>
internal sealed class PendingEvents(StoreContext context) : IPendingEvents
{
    // Every event the library emits, by its name.
    private static readonly FrozenDictionary<string, JsonTypeInfo> Kinds = new JsonTypeInfo[]
    {
        EventJson.Default.AccountDeletionCancelled,
        EventJson.Default.AccountDeletionRequested,
        EventJson.Default.AccountReactivated,
        EventJson.Default.AccountRegistered,
        EventJson.Default.AccountSuspended,
        EventJson.Default.AlertRaised,
        EventJson.Default.ConsentChanged,
        EventJson.Default.CredentialEnrolled,
        EventJson.Default.CredentialInvalidated,
        EventJson.Default.CredentialRestored,
        EventJson.Default.CredentialSuspended,
        EventJson.Default.DeviceVerified,
        EventJson.Default.ErasureRequested,
        EventJson.Default.ExportRequested,
        EventJson.Default.IdentifierAdded,
        EventJson.Default.IdentifierPrimaryChanged,
        EventJson.Default.IdentifierRemoved,
        EventJson.Default.MembershipChanged,
        EventJson.Default.ObjectionChanged,
        EventJson.Default.OrganizationErased,
        EventJson.Default.RestrictionChanged,
        EventJson.Default.SendingRestrictionChanged,
        EventJson.Default.SendingRestrictionGranted,
        EventJson.Default.TakedownExecuted,
        EventJson.Default.TakedownReversed,
    }.ToFrozenDictionary(info => info.Type.Name, StringComparer.Ordinal);

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">The event is not one the library emits.</exception>
    public async ValueTask AddAsync(PendingEvent pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        string kind = pending.Raised.GetType().Name;

        var record = new PendingEventRecord
        {
            Id = pending.Id,
            Kind = kind,
            RaisedAt = pending.Raised.RaisedAt,
            Payload = JsonSerializer.Serialize(pending.Raised, Info(kind)),
            Attempts = pending.Attempts,
            NextAttemptAt = pending.NextAttemptAt,
            TakenBy = TakenBy(pending),
            PublishedAt = pending.PublishedAt,
            FailedAt = pending.FailedAt,
        };

        await context.Events.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PendingEventId>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .Where(pending => pending.PublishedAt == null
                && pending.FailedAt == null
                && pending.NextAttemptAt <= now
                && (pending.ClaimedUntil == null || pending.ClaimedUntil <= now))
            .OrderBy(pending => pending.Id)
            .Select(pending => pending.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async ValueTask<EventClaim?> ClaimAsync(
        PendingEventId pending,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = RowClaim.Until(now, timeout);

        int claimed = await context.Events
            .Where(row => row.Id == pending
                && row.PublishedAt == null
                && row.FailedAt == null
                && row.NextAttemptAt <= now
                && (row.ClaimedUntil == null || row.ClaimedUntil <= now))
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, until),
                cancellationToken)
            .ConfigureAwait(false);

        return claimed == 1 ? new EventClaim(pending, until) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<EventClaim?> RenewAsync(
        EventClaim claim,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = RowClaim.Until(now, timeout);

        int renewed = await context.Events
            .Where(row => row.Id == claim.Event && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, until),
                cancellationToken)
            .ConfigureAwait(false);

        return renewed == 1 ? claim with { Until = until } : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PendingEvent?> FindAsync(PendingEventId pending, CancellationToken cancellationToken) =>
        await context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == pending, cancellationToken)
            .ConfigureAwait(false) is PendingEventRecord row
            ? Read(row)
            : null;

    /// <inheritdoc/>
    public async ValueTask<bool> TakeAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        string taken = TakenBy(pending);

        return await context.Events
            .Where(row => row.Id == claim.Event && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.TakenBy, taken),
                cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RecordAsync(PendingEvent pending, EventClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        int attempts = pending.Attempts;
        DateTimeOffset next = pending.NextAttemptAt;
        string taken = TakenBy(pending);
        DateTimeOffset? published = pending.PublishedAt;
        DateTimeOffset? failed = pending.FailedAt;

        return await context.Events
            .Where(row => row.Id == claim.Event && row.ClaimedUntil == claim.Until)
            .ExecuteUpdateAsync(
                row => row
                    .SetProperty(one => one.Attempts, attempts)
                    .SetProperty(one => one.NextAttemptAt, next)
                    .SetProperty(one => one.TakenBy, taken)
                    .SetProperty(one => one.PublishedAt, published)
                    .SetProperty(one => one.FailedAt, failed)
                    .SetProperty(one => one.ClaimedUntil, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Implements IDN-PRIN-003 AC4: a delivered event is a working artefact once every
    /// consumer has confirmed it. One whose budget was spent was delivered to nobody,
    /// so it stays.
    /// </remarks>
    public async ValueTask<int> SweepAsync(CancellationToken cancellationToken) =>
        await context.Events
            .Where(pending => pending.PublishedAt != null)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

    private static string TakenBy(PendingEvent pending) =>
        JsonSerializer.Serialize(
            pending.Taken.Order(StringComparer.Ordinal).ToList(),
            EventJson.Default.ListString);

    private static PendingEvent Read(PendingEventRecord row) =>
        PendingEvent.Existing(
            row.Id,
            JsonSerializer.Deserialize(row.Payload, Info(row.Kind)) as DomainEvent
                ?? throw new InvalidOperationException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The row '{row.Id}' carries no event.")),
            row.Attempts,
            row.NextAttemptAt,
            JsonSerializer.Deserialize(row.TakenBy, EventJson.Default.ListString) ?? [],
            row.PublishedAt,
            row.FailedAt);

    private static JsonTypeInfo Info(string kind) =>
        Kinds.TryGetValue(kind, out JsonTypeInfo? info)
            ? info
            : throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"'{kind}' is not an event the library emits."));
}
