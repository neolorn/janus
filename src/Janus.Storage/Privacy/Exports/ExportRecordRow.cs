using System;
using Janus.Core;

namespace Janus.Storage.Privacy.Exports;

/// <summary>
/// The <c>privacy_exports</c> row: that an export was assembled for an account and
/// when, which is what the rate limit counts.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-003 and D-086. Nothing of what the export contained is held
/// here: the row would otherwise be a second copy of everything the account holds,
/// kept for a counter.
/// </remarks>
internal sealed class ExportRecordRow
{
    /// <summary>The <c>id</c> column.</summary>
    public Guid Id { get; set; }

    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>assembled_at</c> column.</summary>
    public DateTimeOffset AssembledAt { get; set; }
}
