using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The organizations of the deployment, created, suspended for deletion and restored
/// under <c>organization:manage</c> in the administrative organization.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, IDN-ORG-002, IDN-ORG-003, IDN-ORG-004, AUTH-STEP-002a and
/// chapter 09 section 8a. Every change carries a reason and is audited. A deletion
/// request and its cancellation are the <c>organization:delete</c> step-up action: the
/// request suspends the organization, so no grant of it confers anything, and ends
/// every session of its members in the same transaction. A change of policy is the
/// <c>policy:change</c> step-up action.
/// </remarks>
public interface IOrganizations
{
    /// <summary>
    /// Creates an organization under the system policy, with no override.
    /// </summary>
    /// <param name="context">Who is creating it.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The organization's identifier, or the refusal: <c>api.request.malformed</c>
    /// naming <c>name</c> or <c>reason</c> where either is blank or longer than 1024
    /// characters.
    /// </returns>
    ValueTask<Result<OrganizationId>> CreateAsync(
        AccessContext context,
        string name,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Requests an organization's deletion: it is suspended at once, its members' sessions
    /// end, and it is erased when <c>organization.deletion.grace</c> has run unless the
    /// request is cancelled first. A request for one already being deleted changes
    /// nothing.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.organization.protected</c> for the
    /// administrative organization, <c>api.request.malformed</c> naming <c>id</c> where
    /// the deployment holds no such organization.
    /// </returns>
    ValueTask<Result> RequestDeletionAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels an organization's deletion inside its grace window, which restores it
    /// with every membership and grant it held. A cancellation for one not being deleted
    /// changes nothing.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>identity.deletion.windowelapsed</c> where the window
    /// has closed, <c>api.request.malformed</c> naming <c>id</c> where the deployment
    /// holds no such organization.
    /// </returns>
    ValueTask<Result> CancelDeletionAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads the policy an organization's members resolve to, with the fields and the
    /// gates the organization's own value is in force for.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The policy, or the refusal: <c>api.request.malformed</c> naming <c>id</c> where
    /// the deployment holds no such organization.
    /// </returns>
    ValueTask<Result<OrganizationPolicy>> PolicyAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces what an organization overrides of the system policy. A field the
    /// replacement leaves absent inherits; the domain lock is kept, since it changes
    /// only through the domain operations.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="organization">Which organization.</param>
    /// <param name="replacement">What the organization overrides from now on.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>config.policy.belowsystem</c> where a field is looser
    /// than the system policy, <c>config.value.belowfloor</c> where the administrative
    /// organization would fall below its assurance floor, and
    /// <c>api.request.malformed</c> naming <c>id</c>, <c>reason</c> or
    /// <c>emailDomains</c>.
    /// </returns>
    ValueTask<Result> ReplacePolicyAsync(
        AccessContext context,
        SessionId session,
        OrganizationId organization,
        PolicyOverride replacement,
        string reason,
        CancellationToken cancellationToken);
}
