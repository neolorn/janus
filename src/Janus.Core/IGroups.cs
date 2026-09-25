using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Janus.Core;

/// <summary>
/// The groups of an organization, read, created, removed and given members under
/// <c>group:manage</c> in the organization the group belongs to.
/// </summary>
/// <remarks>
/// Implements LIB-API-005, AUTHZ-GROUP-001, OPS-CFG-007 and chapter 09 section 8a.
/// Every write carries a reason and is audited. A change of members is the
/// <c>grant:manage</c> step-up action, since it confers or takes away what the group
/// holds; where the group holds a role carrying <c>system:administer</c>, directly or
/// through a group it belongs to, the change also needs that permission.
/// </remarks>
public interface IGroups
{
    /// <summary>
    /// Every group of one organization, with the members each holds directly.
    /// </summary>
    /// <param name="context">Who is asking.</param>
    /// <param name="organization">Whose groups.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The groups, or the refusal.</returns>
    ValueTask<Result<IReadOnlyList<DefinedGroup>>> InAsync(
        AccessContext context,
        OrganizationId organization,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a group with no members.
    /// </summary>
    /// <param name="context">Who is creating it.</param>
    /// <param name="organization">The organization it belongs to.</param>
    /// <param name="name">What it is called.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// The group's identifier, or the refusal: <c>api.request.malformed</c> naming
    /// <c>name</c> or <c>reason</c> where either is blank or longer than 1024 characters.
    /// </returns>
    ValueTask<Result<GroupId>> CreateAsync(
        AccessContext context,
        OrganizationId organization,
        string name,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes a group nothing names: it holds no member, belongs to no group, and no
    /// grant was ever given to it.
    /// </summary>
    /// <param name="context">Who is removing it.</param>
    /// <param name="group">Which group.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, or the refusal: <c>authz.group.inuse</c> where anything names it,
    /// <c>api.request.malformed</c> naming <c>id</c> where the deployment holds no such
    /// group.
    /// </returns>
    ValueTask<Result> RemoveAsync(
        AccessContext context,
        GroupId group,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds an account or a group of the same organization to a group; its members
    /// hold what the group holds from the next request.
    /// </summary>
    /// <param name="context">Who is adding it.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="group">The group gaining the member.</param>
    /// <param name="member">The account or group joining it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>
    /// Success, also where it is a member already, or the refusal:
    /// <c>authz.group.cycle</c> where the member already reaches the group.
    /// </returns>
    ValueTask<Result> AddMemberAsync(
        AccessContext context,
        SessionId session,
        GroupId group,
        GrantSubject member,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// Takes an account or a group out of a group; it stops holding what the group
    /// holds from the next request.
    /// </summary>
    /// <param name="context">Who is taking it out.</param>
    /// <param name="session">The session the step-up is judged on.</param>
    /// <param name="group">The group losing the member.</param>
    /// <param name="member">The account or group leaving it.</param>
    /// <param name="reason">Why.</param>
    /// <param name="cancellationToken">Abandons the operation.</param>
    /// <returns>Success, also where it was not a member, or the refusal.</returns>
    ValueTask<Result> RemoveMemberAsync(
        AccessContext context,
        SessionId session,
        GroupId group,
        GrantSubject member,
        string reason,
        CancellationToken cancellationToken);
}
