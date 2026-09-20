using System.Collections.Generic;

namespace Janus.Core;

/// <summary>
/// What a host registers to state which declared purposes it does the work of a
/// consent or an objection change for: on a withdrawal it erases what was held
/// solely for that purpose, and on an objection it stops processing that person for
/// it.
/// </summary>
/// <remarks>
/// Implements PRIV-RIGHT-001a, PRIV-CONS-008 and LIB-HOST-001. The events reach the
/// handler through the host's own <see cref="IEvents"/>, so what the library holds is
/// the registration: a deployment declaring an objectable purpose that no registered
/// handler names does not start.
/// </remarks>
public interface IPurposeHandler
{
    /// <summary>
    /// The purposes this handler does the work for, as the declaration names them.
    /// </summary>
    IReadOnlyCollection<string> Purposes { get; }
}
