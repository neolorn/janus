using System;

namespace Janus.Authentication.Search;

/// <summary>
/// What the staff search writes to the log: a search's result count and duration, and
/// the memory checks, and nothing of a query or an indexed value.
/// </summary>
/// <remarks>Implements IDN-SRCH-002, IDN-SRCH-003 and CONV-LOG-003 (D-256).</remarks>
internal interface ISearchLog
{
    /// <summary>
    /// Records a search, at Debug.
    /// </summary>
    /// <param name="results">How many results it answered.</param>
    /// <param name="duration">How long it took.</param>
    void Searched(int results, TimeSpan duration);
}
