using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Search;
using Janus.Core;

namespace Janus.Authentication.Tests.Search;

/// <summary>
/// Each caller's searches a minute, counted as the table counts them: one row a caller
/// and a minute, the caller's earlier minutes forgotten by the count.
/// </summary>
internal sealed class SearchWindowsInMemory : ISearchWindows
{
    private readonly Dictionary<(SubjectId Subject, DateTimeOffset Window), int> _rows = [];

    /// <summary>
    /// The rows held, by caller and minute.
    /// </summary>
    public IReadOnlyDictionary<(SubjectId Subject, DateTimeOffset Window), int> Rows => _rows;

    /// <inheritdoc/>
    public ValueTask<int> CountAsync(SubjectId subject, DateTimeOffset window, CancellationToken cancellationToken)
    {
        foreach ((SubjectId Subject, DateTimeOffset Window) earlier in _rows.Keys
                     .Where(key => key.Subject == subject && key.Window < window)
                     .ToList())
        {
            _ = _rows.Remove(earlier);
        }

        int counted = _rows.GetValueOrDefault((subject, window)) + 1;
        _rows[(subject, window)] = counted;

        return ValueTask.FromResult(counted);
    }
}
