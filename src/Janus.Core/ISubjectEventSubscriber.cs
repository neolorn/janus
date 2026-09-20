using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a host registers to do its own half of an erasure, a restriction or an
/// export. Delivery is at least once, so a subscriber given the same event twice
/// produces the same result, and one that did not do its work says so rather than
/// throwing.
/// </summary>
/// <remarks>
/// Implements IDN-LIFE-003a and PRIV-RIGHT-005b. Adding a subscriber requires no
/// library change: the library publishes facts about identity and knows nothing of
/// who consumes them.
/// </remarks>
public interface ISubjectEventSubscriber
{
    /// <summary>
    /// What this subscriber is called, which is what its confirmation is recorded
    /// under and what an operator reads when a delivery is outstanding.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether a request stays open until this subscriber confirms. An optional
    /// subscriber failing holds nothing up.
    /// </summary>
    bool Required { get; }

    /// <summary>
    /// Does this subscriber's work for one event.
    /// </summary>
    /// <param name="raised">The event.</param>
    /// <param name="cancellationToken">Abandons the work.</param>
    /// <returns>
    /// Whether the work is done. A failure is delivered again until the retry budget
    /// is spent.
    /// </returns>
    ValueTask<Result> HandleAsync(SubjectEvent raised, CancellationToken cancellationToken);
}
