using System;

namespace Janus.Core;

/// <summary>
/// An organization was created.
/// </summary>
/// <param name="RaisedAt">When it was created.</param>
/// <param name="IdempotencyKey">The key a consumer recognises a repeat by.</param>
/// <param name="Organization">Which organization.</param>
/// <remarks>
/// Implements IDN-ORG-002 and chapter 10 section 5b (D-218). The event is about an
/// organization and no person, so it names no subject.
/// </remarks>
public sealed record OrganizationCreated(
    DateTimeOffset RaisedAt,
    string IdempotencyKey,
    OrganizationId Organization) : DomainEvent(RaisedAt, IdempotencyKey);
