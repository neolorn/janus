using System;
using System.Text.Json;
using System.Threading;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// The staff search's index this process serves, or why it serves none.
/// </summary>
/// <param name="served">Whether the process's registration declares the search.</param>
/// <remarks>
/// Implements IDN-SRCH-003 (D-256). A process that serves the search answers
/// <c>building</c> from its start until its first index is swapped in; an index swapped
/// out for a rebuilt one has its buffers overwritten with zeros at once.
/// </remarks>
internal sealed class SearchIndexHolder(bool served) : IDisposable
{
    private readonly Lock _gate = new();

    private SearchIndex? _index;

    private SearchUnavailability _unavailable = served ? SearchUnavailability.Building : SearchUnavailability.Off;

    /// <summary>
    /// Whether the process's registration declares the search.
    /// </summary>
    public bool Served { get; } = served;

    /// <summary>
    /// The index the search reads.
    /// </summary>
    /// <returns>
    /// The index, or <c>identity.search.unavailable</c> with <c>details.reason</c> saying
    /// why the process serves none.
    /// </returns>
    public Result<SearchIndex> Current()
    {
        lock (_gate)
        {
            return _index is SearchIndex index
                ? Result.Success(index)
                : Result.Failure<SearchIndex>(Error.From(
                    ErrorCodes.SearchUnavailable,
                    "reason",
                    JsonSerializer.SerializeToElement(Reason(_unavailable))));
        }
    }

    /// <summary>
    /// Serves a built index, overwriting the one it replaces.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <exception cref="ArgumentNullException">The index is absent.</exception>
    /// <exception cref="InvalidOperationException">The process does not serve the search.</exception>
    public void Swap(SearchIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        if (!Served)
        {
            throw new InvalidOperationException("The process does not serve the staff search.");
        }

        SearchIndex? replaced;

        lock (_gate)
        {
            replaced = _index;
            _index = index;
        }

        replaced?.Clear();
    }

    /// <summary>
    /// Serves no index, for a reason the process found before building one.
    /// </summary>
    /// <param name="reason">Why.</param>
    public void Withhold(SearchUnavailability reason)
    {
        SearchIndex? replaced;

        lock (_gate)
        {
            replaced = _index;
            _index = null;
            _unavailable = reason;
        }

        replaced?.Clear();
    }

    /// <summary>
    /// Overwrites the index served, as the process stops.
    /// </summary>
    public void Dispose()
    {
        SearchIndex? held;

        lock (_gate)
        {
            held = _index;
            _index = null;
            _unavailable = SearchUnavailability.Off;
        }

        held?.Dispose();
    }

    private static string Reason(SearchUnavailability reason) =>
        reason switch
        {
            SearchUnavailability.Building => "building",
            SearchUnavailability.Capacity => "capacity",
            SearchUnavailability.Unsafe => "unsafe",
            _ => "off",
        };
}
