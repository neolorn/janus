using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// Where the link an account's own lifecycle notice carries is held.
/// </summary>
/// <remarks>
/// Implements CONV-DESIGN-003, IDN-LIFE-013 and IDN-LIFE-014. One link per account:
/// an account is either suspended or deleting, never both, so issuing one replaces
/// whatever stood before it and consuming one leaves none.
/// </remarks>
internal interface ILifecycleLinkStore
{
    /// <summary>
    /// The link a presented token answers to.
    /// </summary>
    /// <param name="fingerprint">What the token hashes to.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The link, or nothing.</returns>
    ValueTask<LifecycleLink?> FindAsync(byte[] fingerprint, CancellationToken cancellationToken);

    /// <summary>
    /// Records one, replacing whatever the account had outstanding.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of recording it.</returns>
    ValueTask ReplaceAsync(LifecycleLink link, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the account's link away, the state it belonged to having ended.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>The work of taking it away.</returns>
    ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken);
}
