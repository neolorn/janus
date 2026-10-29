using System;

namespace Janus.Core;

/// <summary>
/// An email or a phone a person record holds, as it holds it.
/// </summary>
/// <param name="Kind">Whether it is an email or a phone.</param>
/// <param name="Value">The value, in its canonical form.</param>
/// <param name="VerifiedAt">When a code to it was verified at a collection, or nothing.</param>
/// <remarks>Implements REC-ADM-001.</remarks>
public sealed record PersonRecordContact(IdentifierKind Kind, string Value, DateTimeOffset? VerifiedAt);
