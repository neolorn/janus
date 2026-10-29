using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// The <c>record_offers</c> row.
/// </summary>
/// <remarks>
/// Implements REC-ATT-001. The row holds the proven values' keyed fingerprints and
/// never the values, and names the records counted by their subjects alone.
/// </remarks>
internal sealed class RecordOfferRecord
{
    /// <summary>The <c>session_id</c> column, which is this table's key.</summary>
    public SessionId SessionId { get; set; }

    /// <summary>The <c>proven</c> column: the values proved, each by its kind and fingerprints.</summary>
    [NeverLogged]
    public string Proven { get; set; } = "[]";

    /// <summary>The <c>offered</c> column: the subjects of the records counted.</summary>
    public Guid[] Offered { get; set; } = [];

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
