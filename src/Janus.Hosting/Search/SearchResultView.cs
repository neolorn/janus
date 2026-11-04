using System;
using Janus.Core;

namespace Janus.Hosting.Search;

/// <summary>
/// One subject a staff search found, with no email or phone value.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Kind">Whether it is an account or a person record.</param>
/// <param name="DisplayName">Its display name, or null where it has none.</param>
/// <param name="Matched">The field that matched first, in the order name, email, phone.</param>
/// <remarks>Implements IDN-SRCH-001 and chapter 09 section 8a (D-256).</remarks>
internal sealed record SearchResultView(
    Guid Subject,
    SubjectKind Kind,
    [property: NeverLogged] string? DisplayName,
    SearchMatch Matched)
{
    /// <summary>
    /// The view of one result.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>Its view.</returns>
    /// <exception cref="ArgumentNullException">The result is absent.</exception>
    public static SearchResultView Of(SearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SearchResultView(result.Subject.Value, result.Kind, result.DisplayName, result.Matched);
    }
}
