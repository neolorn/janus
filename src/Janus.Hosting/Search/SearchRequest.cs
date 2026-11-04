using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Janus.Core;

namespace Janus.Hosting.Search;

/// <summary>
/// The body of <c>POST /admin/search</c>, which carries the query so that no address a
/// proxy logs holds it.
/// </summary>
/// <param name="Organization">The organization the search is asked in.</param>
/// <param name="Q">The query as typed.</param>
/// <param name="Kinds">The kinds asked for, <c>account</c> and <c>record</c>, or null for both.</param>
/// <param name="Limit">At most how many results, or null for 20.</param>
/// <remarks>
/// Implements IDN-SRCH-001, IDN-SRCH-002, CONV-LOG-003 and chapter 09 section 8a
/// (D-256). A member the body does not take is refused, naming it.
/// </remarks>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SearchRequest(
    Guid? Organization,
    [property: NeverLogged] string? Q,
    IReadOnlyList<string>? Kinds,
    int? Limit);
