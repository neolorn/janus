using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Privacy.Policies;

/// <summary>
/// Where a read of another person is asked, and whether it reaches them from there.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="administrative">Which organization administers the deployment.</param>
/// <param name="subjects">Which organizations are held, and whom each reaches.</param>
/// <remarks>
/// Implements IDN-READ-004 and AUTHZ-CONCEAL-004. A holder in the administrative
/// organization reaches every person, and a holder elsewhere the people tied to its
/// organization; a person beyond the reach is answered as an account no person bears, so
/// the read discloses nothing of whether one exists.
/// </remarks>
internal sealed class SubjectReach(
    IAccessGate gate,
    IAdministrativeOrganization administrative,
    IReachedSubjects subjects)
{
    /// <summary>
    /// Asks the permission in the organization named, or in the administrative
    /// organization where none is named, and whether it reaches the person from there.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The read's permission.</param>
    /// <param name="subject">Whom the read is of.</param>
    /// <param name="named">The organization the read names, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.organization.notfound</c> where the deployment
    /// holds no organization named or erased it, the gate's refusal, or
    /// <c>identity.account.notfound</c> where the person is beyond the reach.
    /// </returns>
    public async ValueTask<Result> ReachedAsync(
        AccessContext context,
        Permission permission,
        SubjectId subject,
        OrganizationId? named,
        CancellationToken cancellationToken)
    {
        // 09 section 8a: an organization the deployment never held, or erased, is
        // answered for what it is before the permission is asked in it; before bootstrap
        // there is no administrative organization to ask it in.
        if (await ScopeOfAsync(named, cancellationToken).ConfigureAwait(false) is not OrganizationId organization)
        {
            return Result.Failure(Error.From(named is null ? ErrorCodes.Denied : ErrorCodes.OrganizationNotFound));
        }

        if ((await gate.RequireAsync(context, permission, organization, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure(refused);
        }

        return await administrative.FindAsync(cancellationToken).ConfigureAwait(false) == organization
            || await subjects.TiedAsync(subject, organization, cancellationToken).ConfigureAwait(false)
            ? Result.Success()
            : Result.Failure(Error.From(ErrorCodes.AccountNotFound));
    }

    // Where the read is asked: the organization named, where the deployment holds it and
    // has not erased it, or the administrative organization where none is named.
    private async ValueTask<OrganizationId?> ScopeOfAsync(
        OrganizationId? named,
        CancellationToken cancellationToken)
    {
        if (named is not OrganizationId organization)
        {
            return await administrative.FindAsync(cancellationToken).ConfigureAwait(false);
        }

        return await subjects.HeldAsync(organization, cancellationToken).ConfigureAwait(false)
            ? organization
            : null;
    }
}
