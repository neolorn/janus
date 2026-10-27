using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Policies;

/// <summary>
/// Which organizations a read of another person may be asked in, and which people each
/// reaches.
/// </summary>
/// <remarks>
/// Implements IDN-READ-004 and CONV-LAYOUT-001. A person is tied to an organization they
/// are a current member of, and to one holding a record registered with them as its data
/// subject.
/// </remarks>
internal interface IReachedSubjects
{
    /// <summary>
    /// Whether the deployment holds an organization and has not erased it.
    /// </summary>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it does.</returns>
    ValueTask<bool> HeldAsync(OrganizationId organization, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a person is tied to an organization.
    /// </summary>
    /// <param name="subject">Who.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether they are.</returns>
    ValueTask<bool> TiedAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken);
}
