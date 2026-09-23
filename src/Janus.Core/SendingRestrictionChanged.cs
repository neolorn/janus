using System;

namespace Janus.Core;

/// <summary>
/// A named restriction was created, edited or deleted.
/// </summary>
/// <param name="RaisedAt">When the change took effect.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Restriction">The name the restriction is edited under.</param>
/// <param name="Loosening">
/// Whether the change loosened the deployment: a higher maximum, a shorter interval,
/// a removed bucket or a deleted restriction.
/// </param>
/// <remarks>Implements AUTH-ABUSE-004, OPS-CFG-008, chapter 10 section 5b.</remarks>
public sealed record SendingRestrictionChanged(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    string Restriction,
    bool Loosening) : DomainEvent(RaisedAt, IdempotencyKey);
