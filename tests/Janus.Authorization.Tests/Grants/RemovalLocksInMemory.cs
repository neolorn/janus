using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Grants;
using Janus.Core;

namespace Janus.Authorization.Tests.Grants;

/// <summary>
/// The organizations whose removals of access were held, in the order they were held.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that keeps what it is given. The unit of work held in memory runs
/// one operation at a time, so holding waits on nothing; the lock itself is tested over
/// the database.
/// </remarks>
internal sealed class RemovalLocksInMemory : IRemovalLocks
{
    private readonly ConcurrentQueue<OrganizationId> _held = new();

    /// <summary>
    /// Every organization held, oldest first.
    /// </summary>
    public IReadOnlyCollection<OrganizationId> Held => _held;

    /// <inheritdoc/>
    public ValueTask HoldAsync(OrganizationId organization, CancellationToken cancellationToken)
    {
        _held.Enqueue(organization);

        return ValueTask.CompletedTask;
    }
}
