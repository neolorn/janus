using System;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// One staged contact, as it is written into the session's encrypted column.
/// </summary>
/// <param name="Kind">Whether it is an email or a phone.</param>
/// <param name="Value">Its canonical value.</param>
/// <param name="VerifiedAt">When its code verified it, where it has.</param>
/// <remarks>Implements REC-COL-001.</remarks>
internal sealed record CollectedContactDocument(string Kind, string Value, DateTimeOffset? VerifiedAt);
