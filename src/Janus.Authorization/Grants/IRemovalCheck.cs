using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Authorization.Grants;

/// <summary>
/// Where the host's check of a removal of access is asked, a check that throws or does
/// not answer refusing the removal.
/// </summary>
/// <remarks>
/// Implements AUTHZ-GRANT-005 (D-281). The authorization area turns no failure into
/// anything (AUTHZ-PRIN-003), so what the host's code throws is caught where the
/// library meets the host, and reaches here as the refusal it stands for.
/// </remarks>
internal interface IRemovalCheck
{
    /// <summary>
    /// Whether the host registered a check, without which nothing is held or asked.
    /// </summary>
    bool Registered { get; }

    /// <summary>
    /// Asks the host's check about one removal.
    /// </summary>
    /// <param name="removal">What is about to be removed.</param>
    /// <param name="cancellationToken">Abandons the change.</param>
    /// <returns>
    /// What the check answered, or <c>authz.removal.uncheckable</c> where it threw or
    /// registered none was asked.
    /// </returns>
    ValueTask<Result> AskAsync(AccessRemoval removal, CancellationToken cancellationToken);
}
