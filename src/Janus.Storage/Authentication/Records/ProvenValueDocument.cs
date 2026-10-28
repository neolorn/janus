using System.Collections.Generic;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// One value an offer to attach was proved by, as it is written into the offer's row.
/// </summary>
/// <param name="Kind">Whether it is an email or a phone.</param>
/// <param name="Fingerprints">The value's keyed fingerprints, never the value.</param>
/// <remarks>Implements REC-ATT-001.</remarks>
internal sealed record ProvenValueDocument(string Kind, IReadOnlyList<byte[]> Fingerprints);
