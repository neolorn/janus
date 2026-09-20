using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authentication.Accounts;
using Janus.Core;

namespace Janus.Authentication.Tests.Accounts;

/// <summary>
/// The links the lifecycle notices carried, in a list, so a test can read back what
/// an account holds and prove that consuming one leaves none.
/// </summary>
internal sealed class LifecycleLinkStoreInMemory : ILifecycleLinkStore
{
    private readonly List<LifecycleLink> _links = [];

    /// <summary>
    /// What is outstanding.
    /// </summary>
    public IReadOnlyList<LifecycleLink> Links => _links;

    /// <inheritdoc/>
    public ValueTask<LifecycleLink?> FindAsync(
        byte[] fingerprint,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_links.Find(link => link.Token.AsSpan().SequenceEqual(fingerprint)));

    /// <inheritdoc/>
    public ValueTask ReplaceAsync(LifecycleLink link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);

        _ = _links.RemoveAll(held => held.Subject == link.Subject);
        _links.Add(link);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask RemoveAsync(SubjectId subject, CancellationToken cancellationToken)
    {
        _ = _links.RemoveAll(held => held.Subject == subject);

        return ValueTask.CompletedTask;
    }
}
