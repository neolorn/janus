using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Alerting;
using Janus.Core;
using Microsoft.EntityFrameworkCore;

namespace Janus.Storage.Authentication.Alerting;

/// <summary>
/// Where the raised conditions wait for the alert channels.
/// </summary>
/// <param name="context">The context the operation runs on.</param>
/// <remarks>Implements OPS-ALERT-001, CONV-DESIGN-002 and CONV-DESIGN-003.</remarks>
internal sealed class RaisedAlerts(StoreContext context) : IRaisedAlerts
{
    /// <inheritdoc/>
    public async ValueTask AddAsync(RaisedAlert alert, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alert);

        var record = new RaisedAlertRecord
        {
            Id = alert.Id,
            RaisedAt = alert.Raised.RaisedAt,
            IdempotencyKey = alert.Raised.IdempotencyKey,
            Condition = alert.Raised.Condition,
            Scope = alert.Raised.Scope,
            Details = JsonSerializer.Serialize(
                new Dictionary<string, JsonElement>(alert.Raised.Details, StringComparer.Ordinal),
                RaisedAlertJson.Default.DictionaryStringJsonElement),
        };

        await context.RaisedAlerts.AddAsync(record, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RaisedAlert>> OldestAsync(
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken)
    {
        List<RaisedAlertRecord> rows = await context.RaisedAlerts
            .AsNoTracking()
            .Where(alert => alert.ClaimedUntil == null || alert.ClaimedUntil <= now)
            .OrderBy(alert => alert.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(Read)];
    }

    /// <inheritdoc/>
    public async ValueTask<DateTimeOffset?> ClaimAsync(
        RaisedAlertId alert,
        DateTimeOffset now,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DateTimeOffset until = RowClaim.Until(now, timeout);

        int claimed = await context.RaisedAlerts
            .Where(row => row.Id == alert && (row.ClaimedUntil == null || row.ClaimedUntil <= now))
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, until),
                cancellationToken)
            .ConfigureAwait(false);

        return claimed == 1 ? until : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> RemoveAsync(
        RaisedAlertId alert,
        DateTimeOffset claim,
        CancellationToken cancellationToken) =>
        await context.RaisedAlerts
            .Where(row => row.Id == alert && row.ClaimedUntil == claim)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false) == 1;

    /// <inheritdoc/>
    public async ValueTask<bool> ReleaseAsync(
        RaisedAlertId alert,
        DateTimeOffset claim,
        CancellationToken cancellationToken) =>
        await context.RaisedAlerts
            .Where(row => row.Id == alert && row.ClaimedUntil == claim)
            .ExecuteUpdateAsync(
                row => row.SetProperty(one => one.ClaimedUntil, (DateTimeOffset?)null),
                cancellationToken)
            .ConfigureAwait(false) == 1;

    private static RaisedAlert Read(RaisedAlertRecord row) =>
        new(
            row.Id,
            new AlertRaised(
                row.RaisedAt,
                row.IdempotencyKey,
                row.Condition,
                Alerts.Severity(row.Condition),
                JsonSerializer.Deserialize(row.Details, RaisedAlertJson.Default.DictionaryStringJsonElement)
                    ?? throw new InvalidOperationException("The stored details are not a document."))
            {
                Scope = row.Scope,
            });
}
