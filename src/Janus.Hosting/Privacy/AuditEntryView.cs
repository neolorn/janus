using System;
using System.Collections.Generic;
using System.Text.Json;
using Janus.Core;

namespace Janus.Hosting.Privacy;

/// <summary>
/// One audit record of a subject, as the trail is read back.
/// </summary>
/// <param name="Id">The record's own identifier.</param>
/// <param name="Category">Which retention it falls under.</param>
/// <param name="Action">What happened, as a code.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="Acting">Who took the action.</param>
/// <param name="Effective">Whose identity the action was taken under.</param>
/// <param name="Organization">The organization it belongs to, where one applies.</param>
/// <param name="Details">The codes and references it carries.</param>
/// <remarks>Implements PRIV-BREACH-002 and chapter 09 section 8a.</remarks>
internal sealed record AuditEntryView(
    Guid Id,
    AuditCategory Category,
    string Action,
    DateTimeOffset OccurredAt,
    Guid Acting,
    Guid Effective,
    Guid? Organization,
    IReadOnlyDictionary<string, JsonElement> Details)
{
    /// <summary>
    /// The view of an audit record.
    /// </summary>
    /// <param name="entry">The record.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The record is absent.</exception>
    public static AuditEntryView Of(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new AuditEntryView(
            entry.Id.Value,
            entry.Category,
            entry.Action.ToString(),
            entry.OccurredAt,
            entry.Acting.Value,
            entry.Effective.Value,
            entry.Organization?.Value,
            entry.Details);
    }
}
