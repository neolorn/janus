using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// Whether an account is tied to an organization, which is how far a read asked there
/// reaches.
/// </summary>
/// <remarks>
/// Implements IDN-READ-004 and CONV-LAYOUT-001. An account is tied to an organization
/// it is a current member of, and to one holding a record registered with the account
/// as its data subject.
/// </remarks>
internal interface IAccountTies
{
    /// <summary>
    /// Whether the account is tied to the organization.
    /// </summary>
    /// <param name="subject">Whose account.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it is.</returns>
    ValueTask<bool> TiedAsync(
        SubjectId subject,
        OrganizationId organization,
        CancellationToken cancellationToken);
}
