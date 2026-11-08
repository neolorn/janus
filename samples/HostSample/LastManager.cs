using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HostSample;

/// <summary>
/// The workshop's rule on taking access away: an organization keeps at least one manager,
/// so a grant's revocation or a membership's end that would leave it none is refused,
/// whichever way it comes (AUTHZ-GRANT-005).
/// </summary>
/// <param name="scopes">Where each check's scope is made.</param>
/// <param name="time">The clock the workshop runs on.</param>
/// <remarks>
/// The library holds every removal of one organization until the one before it is
/// committed, so the managers counted are those every earlier removal left. Taking a
/// member out of a group leaves the group's grants where they are, so it is not judged
/// here.
/// </remarks>
internal sealed class LastManager(IServiceScopeFactory scopes, TimeProvider time) : IAccessRemovalCheck
{
    /// <summary>
    /// The code a removal that would leave an organization no manager is refused with.
    /// </summary>
    public static ErrorCode Kept { get; } = ErrorCode.Parse("workshop.lastmanager");

    /// <inheritdoc/>
    public async ValueTask<Result> CheckAsync(AccessRemoval removal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removal);

        Guid[] removed = removal.Kind switch
        {
            AccessRemovalKind.GrantRevoked when removal.Grant is GrantId grant
                && removal.Role?.ToString() == WorkshopRoles.Manager => [grant.Value],
            AccessRemovalKind.MembershipEnded =>
            [
                .. removal.Grants
                    .Where(held => held.Role.ToString() == WorkshopRoles.Manager)
                    .Select(held => held.Grant.Value),
            ],
            _ => [],
        };

        if (removed.Length == 0)
        {
            return Result.Success();
        }

        AsyncServiceScope scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            WorkshopContext context = scope.ServiceProvider.GetRequiredService<WorkshopContext>();
            DateTimeOffset now = time.GetUtcNow();
            Guid organization = removal.Organization.Value;

            bool left = await context.Grants
                .AnyAsync(
                    row => row.Role == WorkshopRoles.Manager
                        && row.Organization == organization
                        && row.ResourceType == null
                        && !row.Deny
                        && row.RevokedAt == null
                        && (row.ExpiresAt == null || row.ExpiresAt > now)
                        && !removed.Contains(row.GrantId),
                    cancellationToken)
                .ConfigureAwait(false);

            return left ? Result.Success() : Result.Failure(Error.From(Kept));
        }
    }
}
