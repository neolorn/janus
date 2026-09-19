using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.SubjectKeys;

/// <summary>
/// Where subject keys are read and written.
/// </summary>
/// <remarks>Implements PRIV-RIGHT-005a and CONV-DESIGN-003.</remarks>
internal interface ISubjectKeyStore
{
    /// <summary>
    /// Reads one subject's key.
    /// </summary>
    /// <param name="subject">Whose key to read.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The key, or nothing where the subject has none.</returns>
    ValueTask<SubjectKey?> FindBySubjectAsync(SubjectId subject, CancellationToken cancellationToken);

    /// <summary>
    /// Records a subject's key. It is written in the transaction that creates the
    /// subject, so no subject exists without one.
    /// </summary>
    /// <param name="key">The key to record.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask AddAsync(SubjectKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Carries the wrapping the key now holds onto the row. Re-wrapping under a newer
    /// key-encryption key version and erasure both reach the row this way; which of the
    /// two happened is the key's business, not the store's.
    /// </summary>
    /// <param name="key">The key as it now stands.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording the wrapping.</returns>
    /// <exception cref="System.InvalidOperationException">The subject has no key row.</exception>
    ValueTask RecordWrappingAsync(SubjectKey key, CancellationToken cancellationToken);
}
