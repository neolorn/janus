using System.Collections.Generic;

namespace Janus.Hosting.Search;

/// <summary>
/// What a staff search answers: its results, sorted and capped, with no paging.
/// </summary>
/// <param name="Results">The results.</param>
/// <remarks>Implements IDN-SRCH-001 and chapter 09 section 8a (D-256).</remarks>
internal sealed record SearchResultsView(IReadOnlyList<SearchResultView> Results);
