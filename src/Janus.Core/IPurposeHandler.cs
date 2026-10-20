using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// What a host registers to do the work of a consent or an objection change for the
/// declared purposes it names: on a withdrawal it erases what was held solely for that
/// purpose, and on an objection it stops processing that person for it. Delivery is at
/// least once, so a handler given the same change twice produces the same result, and
/// one that did not do its work says so rather than throwing.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001a, PRIV-CONS-008, IDN-LIFE-003a and LIB-HOST-001. Each
/// change is delivered through the transactional outbox, as a subject event is, and a
/// handler's success confirms its work. Every handler is required: a delivery stays
/// open until each handler naming its purpose has confirmed it, and one whose attempts
/// are spent is raised and completed by hand. A deployment declaring a purpose resting
/// on consent or on an objectable basis that no registered handler names does not
/// start.
/// </remarks>
public interface IPurposeHandler
{
    /// <summary>
    /// What this handler is called, which is what its confirmation is recorded under
    /// and what an operator reads when a delivery is outstanding. It is held to the
    /// rule of a message place and is distinct among the purpose handlers and the
    /// subject-event subscribers together.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The purposes this handler does the work for, as the declaration names them.
    /// </summary>
    IReadOnlyCollection<string> Purposes { get; }

    /// <summary>
    /// Does this handler's work for one consent change of a purpose it names: on a
    /// withdrawal, erases what was held solely for that purpose.
    /// </summary>
    /// <param name="changed">The change.</param>
    /// <param name="cancellationToken">Abandons the work.</param>
    /// <returns>
    /// Whether the work is done. A failure is delivered again until the retry budget
    /// is spent.
    /// </returns>
    ValueTask<Result> ConsentChangedAsync(ConsentChanged changed, CancellationToken cancellationToken);

    /// <summary>
    /// Does this handler's work for one objection change of a purpose it names: on an
    /// objection, stops processing that person for it.
    /// </summary>
    /// <param name="changed">The change.</param>
    /// <param name="cancellationToken">Abandons the work.</param>
    /// <returns>
    /// Whether the work is done. A failure is delivered again until the retry budget
    /// is spent.
    /// </returns>
    ValueTask<Result> ObjectionChangedAsync(ObjectionChanged changed, CancellationToken cancellationToken);
}
