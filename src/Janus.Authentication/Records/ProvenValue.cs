using System.Collections.Generic;
using Janus.Core;

namespace Janus.Authentication.Records;

/// <summary>
/// An email or a phone an account proved now, as an offer to attach holds it: its kind
/// and its keyed fingerprints, never the value.
/// </summary>
/// <param name="Kind">Whether it is an email or a phone.</param>
/// <param name="Fingerprints">The value's keyed fingerprints.</param>
/// <remarks>Implements REC-ATT-001.</remarks>
internal sealed record ProvenValue(IdentifierKind Kind, IReadOnlyList<byte[]> Fingerprints);
