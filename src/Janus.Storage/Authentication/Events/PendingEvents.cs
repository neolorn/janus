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
/// so a retry days later offers what the transaction raised.
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
        EventJson.Default.NotificationRequested,
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
        };

        Written(record, pending);

        await context.Events.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PendingEvent>> DueAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken)
    {
        List<PendingEventRecord> rows = await context.Events
            .AsNoTracking()
            .Where(pending => pending.PublishedAt == null
                && pending.FailedAt == null
                && pending.NextAttemptAt <= now)
            .OrderBy(pending => pending.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(Read)];
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">No row holds the event.</exception>
    public async ValueTask RecordAsync(PendingEvent pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);

        PendingEventRecord record = await context.Events
            .FindAsync([pending.Id], cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"No row holds the event '{pending.Id}'."));

        Written(record, pending);
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

    private static void Written(PendingEventRecord record, PendingEvent pending)
    {
        record.Attempts = pending.Attempts;
        record.NextAttemptAt = pending.NextAttemptAt;
        record.TakenBy = JsonSerializer.Serialize(
            pending.Taken.Order(StringComparer.Ordinal).ToList(),
            EventJson.Default.ListString);
        record.PublishedAt = pending.PublishedAt;
        record.FailedAt = pending.FailedAt;
    }

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
