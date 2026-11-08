using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Janus.Core;
using Npgsql;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's check of a removal of access, as a host writes one: it answers nothing for
/// an organization no case armed, and for one a case armed, what the case gave it.
/// </summary>
/// <param name="connectionString">Where the host reads the grants the deployment holds.</param>
/// <remarks>
/// CONV-TEST-004: a fake that keeps every removal it is asked about, so a case reads back
/// which ones reached it. Each case arms the organization it writes, so cases running at
/// once never answer for one another (AUTHZ-GRANT-005, D-281).
/// </remarks>
internal sealed class ArmedRemovalCheck(string connectionString) : IAccessRemovalCheck
{
    private readonly ConcurrentDictionary<OrganizationId, Func<AccessRemoval, CancellationToken, ValueTask<Result>>> _armed = new();

    private readonly ConcurrentQueue<AccessRemoval> _asked = new();

    /// <summary>
    /// Every removal asked about in an organization, oldest first.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <returns>The removals.</returns>
    public IReadOnlyList<AccessRemoval> AskedIn(OrganizationId organization) =>
        [.. _asked.Where(removal => removal.Organization == organization)];

    /// <summary>
    /// Answers every removal in an organization as given.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="answer">What the check answers.</param>
    public void Arm(OrganizationId organization, Func<AccessRemoval, CancellationToken, ValueTask<Result>> answer) =>
        _armed[organization] = answer;

    /// <summary>
    /// Keeps the last live allow grant of a role in an organization: a removal that would
    /// leave none is refused with the host's code.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="role">The role whose last holder is kept.</param>
    /// <param name="code">What the refusal carries.</param>
    public void KeepLast(OrganizationId organization, RoleName role, ErrorCode code) =>
        Arm(organization, async (removal, cancellationToken) =>
        {
            GrantId[] removed =
            [
                .. removal.Grants.Where(grant => grant.Role == role).Select(grant => grant.Grant),
                .. removal.Grant is GrantId grant && removal.Role == role ? [grant] : Array.Empty<GrantId>(),
            ];

            if (removed.Length == 0)
            {
                return Result.Success();
            }

            await using var connection = new NpgsqlConnection(connectionString);

            int left = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                SELECT count(*)::int FROM identity.grants
                WHERE organization = @organization AND role = @role AND NOT deny
                  AND revoked_at IS NULL AND (expires_at IS NULL OR expires_at > @at)
                  AND NOT (id = ANY(@removed))
                """,
                new
                {
                    organization = removal.Organization.Value,
                    role = role.ToString(),
                    at = Deployment.Noon,
                    removed = removed.Select(held => held.Value).ToArray(),
                },
                cancellationToken: cancellationToken));

            return left > 0 ? Result.Success() : Result.Failure(Error.From(code));
        });

    /// <inheritdoc/>
    public async ValueTask<Result> CheckAsync(AccessRemoval removal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removal);

        _asked.Enqueue(removal);

        return _armed.TryGetValue(removal.Organization, out Func<AccessRemoval, CancellationToken, ValueTask<Result>>? answer)
            ? await answer(removal, cancellationToken)
            : Result.Success();
    }
}
