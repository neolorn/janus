using System;
using Janus.Authentication.Records;
using Janus.Core;

namespace Janus.Storage.Authentication.Records;

/// <summary>
/// The <c>record_access_sessions</c> row.
/// </summary>
/// <remarks>
/// Implements REC-ACC-001. The row holds the value's keyed fingerprints and never the
/// value, and names the records reached by their subjects alone.
/// </remarks>
internal sealed class RecordAccessSessionRecord
{
    /// <summary>The <c>id</c> column, which is this table's key.</summary>
    public RecordAccessSessionId Id { get; set; }

    /// <summary>The <c>kind</c> column: whether the value is an email or a phone.</summary>
    public IdentifierKind Kind { get; set; }

    /// <summary>
    /// The <c>fingerprints</c> column: the value's keyed fingerprints, one after another.
    /// </summary>
    [NeverLogged]
    public byte[] Fingerprints { get; set; } = [];

    /// <summary>
    /// The <c>throttled</c> column: what the value's tries are counted against.
    /// </summary>
    [NeverLogged]
    public byte[] Throttled { get; set; } = [];

    /// <summary>The <c>created_at</c> column.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The <c>opened_at</c> column, set once the code opened the session.</summary>
    public DateTimeOffset? OpenedAt { get; set; }

    /// <summary>The <c>expires_at</c> column, which the sweep reads.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>The <c>reached</c> column: the subjects of the records reached.</summary>
    public Guid[] Reached { get; set; } = [];
}
