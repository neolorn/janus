using System;
using System.Collections.Generic;
using Janus.Authentication.Search;

namespace Janus.Authentication.Tests.Search;

/// <summary>
/// What the staff search wrote to the log, kept for the test to read.
/// </summary>
internal sealed class SearchLogInMemory : ISearchLog
{
    private readonly List<(int Results, TimeSpan Duration)> _searched = [];

    /// <summary>
    /// Each search recorded, in order.
    /// </summary>
    public IReadOnlyList<(int Results, TimeSpan Duration)> Searched => _searched;

    /// <inheritdoc/>
    void ISearchLog.Searched(int results, TimeSpan duration) => _searched.Add((results, duration));
}
