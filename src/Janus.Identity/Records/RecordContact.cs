using System;
using Janus.Core;

namespace Janus.Identity.Records;

/// <summary>
/// An email or a phone a person record holds, in its canonical form, with the instant
/// it was last verified at a collection.
/// </summary>
/// <param name="Kind">An email or a phone.</param>
/// <param name="Value">The canonical form (IDN-ACCT-004).</param>
/// <param name="VerifiedAt">When it was last verified at a collection, where it was.</param>
/// <remarks>
/// Implements REC-REC-001. A record holds at most one contact of each kind; the value is
/// held under the record's key with a keyed fingerprint beside it (PRIV-RIGHT-005c).
/// </remarks>
internal sealed record RecordContact(IdentifierKind Kind, string Value, DateTimeOffset? VerifiedAt);
