using System;

namespace Janus.Core;

/// <summary>
/// An organization's deletion was cancelled inside its grace window, which stands it back up.
/// </summary>
/// <param name="RaisedAt">When the deletion was cancelled.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Organization">Which organization.</param>
/// <remarks>
/// Implements IDN-ORG-003 and chapter 10 section 5b (D-218). The event is about an
/// organization and no person, so it names no subject.
/// </remarks>
public sealed record OrganizationDeletionCancelled(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    OrganizationId Organization) : DomainEvent(RaisedAt, IdempotencyKey);
