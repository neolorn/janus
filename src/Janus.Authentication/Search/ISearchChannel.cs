using System;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Search;

/// <summary>
/// The database channel every write that changes what the staff search holds is
/// announced on, carrying the subject alone.
/// </summary>
/// <remarks>Implements IDN-SRCH-003 and chapter 10 section 5.54 (D-256).</remarks>
internal interface ISearchChannel
{
    /// <summary>
    /// Listens on the channel.
    /// </summary>
    /// <param name="changed">Called with each subject a write announced.</param>
    /// <param name="cancellationToken">Ends the listening.</param>
    /// <returns>
    /// Once the channel is listened on, the work of listening, which ends when the
    /// listening connection is lost or the token ends it; ended already, with its fault,
    /// where the channel could not be listened on.
    /// </returns>
    ValueTask<Task> ListenAsync(Action<SubjectId> changed, CancellationToken cancellationToken);
}
