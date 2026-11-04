using System;
using Janus.Core;

namespace Janus.Storage.Authentication.Search;

/// <summary>
/// The <c>search_windows</c> row: how many staff searches one account made in one
/// minute.
/// </summary>
/// <remarks>Implements IDN-SRCH-002 (D-256). Nothing of a query is held here.</remarks>
internal sealed class SearchWindowRecord
{
    /// <summary>The <c>subject</c> column.</summary>
    public SubjectId Subject { get; set; }

    /// <summary>The <c>window_start</c> column.</summary>
    public DateTimeOffset WindowStart { get; set; }

    /// <summary>The <c>count</c> column.</summary>
    public int Count { get; set; }
}
