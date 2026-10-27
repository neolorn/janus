using System;

namespace Janus.Core;

/// <summary>
/// An organization's deletion was requested, which suspends it until the request is cancelled or its grace window elapses.
/// </summary>
/// <param name="RaisedAt">When the deletion was requested.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Organization">Which organization.</param>
/// <remarks>
/// Implements IDN-ORG-003 and chapter 10 section 5b (D-218). The event is about an
/// organization and no person, so it names no subject.
/// </remarks>
public sealed record OrganizationDeletionRequested(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    OrganizationId Organization) : DomainEvent(RaisedAt, IdempotencyKey);
