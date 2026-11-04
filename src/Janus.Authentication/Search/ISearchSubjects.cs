using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// Where the staff search reads the subjects it indexes: every account not erased or
/// taken down and every such person record.
/// </summary>
/// <remarks>Implements IDN-SRCH-001 to IDN-SRCH-003 and CONV-DESIGN-003 (D-256).</remarks>
internal interface ISearchSubjects
{
    /// <summary>
    /// How many subjects there are to index.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The count.</returns>
    ValueTask<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every subject to index, decrypted.
    /// </summary>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The subjects.</returns>
    ValueTask<IReadOnlyList<SearchSubject>> AllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One subject as it now stands.
    /// </summary>
    /// <param name="subject">The subject a change named.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The subject, or nothing where it is not to be indexed: erased, taken down or unknown.</returns>
    ValueTask<SearchSubject?> FindAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// The display names of the subjects a search found, read at the search.
    /// </summary>
    /// <param name="subjects">The subjects found.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>
    /// Each subject still to be indexed with its display name, or nothing where it has
    /// none; a subject erased or taken down since the index last heard of it is absent.
    /// </returns>
    ValueTask<IReadOnlyDictionary<SubjectId, string?>> NamesAsync(
        IReadOnlyList<SubjectId> subjects,
        CancellationToken cancellationToken);
}
