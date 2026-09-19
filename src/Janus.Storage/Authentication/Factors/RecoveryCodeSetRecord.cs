using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Factors;

/// <summary>
/// The <c>recovery_code_sets</c> row.
/// </summary>
/// <remarks>
/// Implements AUTH-FACT-008 and CONV-DESIGN-003. The set is the row the timestamps
/// hang from; the codes themselves are its children.
/// </remarks>
internal sealed class RecoveryCodeSetRecord
{
    /// <summary>
    /// The <c>subject</c> column, which is this table's key: an account holds one set.
    /// </summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>generated_at</c> column.</summary>
    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>The <c>viewed_at</c> column.</summary>
    public DateTimeOffset? ViewedAt { get; set; }

    /// <summary>The <c>exported_at</c> column.</summary>
    public DateTimeOffset? ExportedAt { get; set; }

    /// <summary>The <c>reminded_at</c> column.</summary>
    public DateTimeOffset? RemindedAt { get; set; }
}
