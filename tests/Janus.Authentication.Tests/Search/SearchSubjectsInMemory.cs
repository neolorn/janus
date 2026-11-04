using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Search;
using Janus.Core;

namespace Janus.Authentication.Tests.Search;

/// <summary>
/// The subjects a test holds for the staff search to index: each as the database would
/// answer it, an erased or taken-down one absent. A test changes them while the index's
/// keeping reads them, so each read and change is taken under one lock.
/// </summary>
internal sealed class SearchSubjectsInMemory : ISearchSubjects
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SubjectId, SearchSubject> _held = [];

    /// <summary>
    /// Holds a subject as it now stands.
    /// </summary>
    /// <param name="subject">The subject.</param>
    public void Hold(SearchSubject subject)
    {
        lock (_gate)
        {
            _held[subject.Subject] = subject;
        }
    }

    /// <summary>
    /// Erases a subject, so nothing answers it.
    /// </summary>
    /// <param name="subject">The subject.</param>
    public void Erase(SubjectId subject)
    {
        lock (_gate)
        {
            _ = _held.Remove(subject);
        }
    }

    /// <inheritdoc/>
    public ValueTask<int> CountAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_held.Count);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SearchSubject>> AllAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<IReadOnlyList<SearchSubject>>([.. _held.Values]);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SearchSubject?> FindAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult(_held.GetValueOrDefault(subject));
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyDictionary<SubjectId, string?>> NamesAsync(
        IReadOnlyList<SubjectId> subjects,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<IReadOnlyDictionary<SubjectId, string?>>(
                subjects
                    .Where(_held.ContainsKey)
                    .ToDictionary(subject => subject, subject => _held[subject].DisplayName));
        }
    }

    /// <summary>
    /// An index built over every subject held.
    /// </summary>
    /// <returns>The index.</returns>
    public SearchIndex Indexed()
    {
        lock (_gate)
        {
            return SearchIndex.Build([.. _held.Values.Select(IndexedSubject.Of)]);
        }
    }
}
