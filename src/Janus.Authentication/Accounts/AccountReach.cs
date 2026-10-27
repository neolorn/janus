using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Organizations;
using Janus.Authentication.Policies;
using Janus.Core;

namespace Janus.Authentication.Accounts;

/// <summary>
/// Where a read of another person's account is asked, and which accounts it reaches
/// from there.
/// </summary>
/// <param name="gate">The one place a permission is evaluated.</param>
/// <param name="administrative">Which organization administers the deployment.</param>
/// <param name="organizations">Where the organization named is found.</param>
/// <param name="ties">Which accounts an organization reaches.</param>
/// <remarks>
/// Implements IDN-READ-004 and AUTHZ-CONCEAL-004. A holder in the administrative
/// organization reaches every account, and a holder elsewhere the accounts tied to its
/// organization; an account beyond the reach is answered by the caller as one no person
/// bears, so the read discloses nothing of whether it exists.
/// </remarks>
internal sealed class AccountReach(
    IAccessGate gate,
    IAdministrativeOrganization administrative,
    IOrganizationDirectory organizations,
    IAccountTies ties)
{
    /// <summary>
    /// Asks the permission in the organization named, or in the administrative
    /// organization where none is named.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="permission">The read's permission.</param>
    /// <param name="named">The organization the read names, or nothing.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Where the read was admitted, or the refusal: <c>identity.organization.notfound</c>
    /// where the deployment holds no organization named or erased it, the gate's refusal
    /// otherwise.
    /// </returns>
    public async ValueTask<Result<AdmittedRead>> AskedAsync(
        AccessContext context,
        Permission permission,
        OrganizationId? named,
        CancellationToken cancellationToken)
    {
        // 09 section 8a: an organization the deployment never held, or erased, is
        // answered for what it is before the permission is asked in it; before bootstrap
        // there is no administrative organization to ask it in.
        if (await ScopeOfAsync(named, cancellationToken).ConfigureAwait(false) is not OrganizationId organization)
        {
            return Result.Failure<AdmittedRead>(
                Error.From(named is null ? ErrorCodes.Denied : ErrorCodes.OrganizationNotFound));
        }

        if ((await gate.RequireAsync(context, permission, organization, cancellationToken).ConfigureAwait(false))
            .Match<Error?>(() => null, error => error) is Error refused)
        {
            return Result.Failure<AdmittedRead>(refused);
        }

        return Result.Success(new AdmittedRead(
            organization,
            await administrative.FindAsync(cancellationToken).ConfigureAwait(false) == organization));
    }

    /// <summary>
    /// Whether a read admitted in one organization reaches an account.
    /// </summary>
    /// <param name="read">Where the read was admitted.</param>
    /// <param name="subject">Whose account.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Whether it does.</returns>
    public async ValueTask<bool> ReachesAsync(
        AdmittedRead read,
        SubjectId subject,
        CancellationToken cancellationToken) =>
        read.Administrative
        || await ties.TiedAsync(subject, read.Organization, cancellationToken).ConfigureAwait(false);

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

        return await organizations.FindAsync(organization, cancellationToken).ConfigureAwait(false)
            is { ErasedAt: null }
            ? organization
            : null;
    }
}
