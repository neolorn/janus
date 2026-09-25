using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Janus.Core;

/// <summary>
/// One audit record as the trail is read back by subject: what happened, when, who
/// acted, whose identity it was taken under, and the codes and references it carries.
/// </summary>
/// <param name="Id">The record's own identifier.</param>
/// <param name="Category">Which retention it falls under.</param>
/// <param name="Action">What happened, as a code.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="Acting">Who took the action.</param>
/// <param name="Effective">Whose identity the action was taken under.</param>
/// <param name="Organization">The organization it belongs to, where one applies.</param>
/// <param name="Details">The codes and references it carries.</param>
/// <remarks>
/// Implements PRIV-BREACH-002 and IDN-AUD-001. What a record holds under the subject's
/// key is not part of the entry, so an entry reads the same before and after erasure
/// and a reader of the trail is handed no personal value.
/// </remarks>
public sealed record AuditEntry(
    AuditRecordId Id,
    AuditCategory Category,
    AuditAction Action,
    DateTimeOffset OccurredAt,
    SubjectId Acting,
    SubjectId Effective,
    OrganizationId? Organization,
    IReadOnlyDictionary<string, JsonElement> Details);
