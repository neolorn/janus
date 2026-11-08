using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's rule on taking access away: an organization keeps at least one manager,
/// so a grant's revocation or a membership's end that would leave nobody else managing
/// its grants is refused, whichever way it comes (AUTHZ-GRANT-005).
/// </summary>
/// <param name="scopes">Where each check's scope is made.</param>
/// <remarks>
/// Who manages an organization's grants is asked of the gate, as the person making the
/// change; the library holds every removal of one organization until the one before it
/// is committed, so the answer is what every earlier removal left. An end the job carries
/// out was judged when it was set, and taking a member out of a group leaves the group's
/// grants where they are, so neither is judged here.
/// </remarks>
internal sealed class LastManager(IServiceScopeFactory scopes) : IAccessRemovalCheck
{
    /// <summary>
    /// The code a removal that would leave an organization no manager is refused with.
    /// </summary>
    public static ErrorCode Kept { get; } = ErrorCode.Parse("workshop.lastmanager");

    /// <summary>
    /// The code a removal is refused with where the person making it may not read who
    /// manages the organization.
    /// </summary>
    public static ErrorCode Unchecked { get; } = ErrorCode.Parse("workshop.managersunread");

    /// <inheritdoc/>
    public async ValueTask<Result> CheckAsync(AccessRemoval removal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removal);

        (AccessRemovalKind kind, _, GrantSubject subject, _, RoleName? role, _, AccessContext context, _, _, IReadOnlyList<RemovedGrant> revoked, _) = removal;

        bool reaches = kind switch
        {
            AccessRemovalKind.GrantRevoked => role?.ToString() == WorkshopRoles.Manager,
            AccessRemovalKind.MembershipEnded => revoked.Any(held => held.Role.ToString() == WorkshopRoles.Manager),
            _ => false,
        };

        if (!reaches || context.Acting is null)
        {
            return Result.Success();
        }

        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            Result<AdmittedAccounts> managing = await scope.ServiceProvider
                .GetRequiredService<IAccessGate>()
                .WhoMayAsync(
                    context,
                    Permissions.GrantManage,
                    new ResourceReference(ResourceType.Parse("organization"), ResourceId.Parse(removal.Organization.ToString())),
                    cancellationToken)
                .ConfigureAwait(false);

            return managing.Match(
                admitted => admitted.Subjects.Any(other => GrantSubject.Of(other) != subject)
                    ? Result.Success()
                    : Result.Failure(Error.From(Kept)),
                _ => Result.Failure(Error.From(Unchecked)));
        }
    }
}
