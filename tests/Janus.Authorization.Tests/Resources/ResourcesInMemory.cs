using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Janus.Authorization.Resources;
using Janus.Core;

namespace Janus.Authorization.Tests.Resources;

/// <summary>
/// The records a host registered, held in memory with the container each sits in.
/// </summary>
/// <remarks>
/// CONV-TEST-004: a fake that answers from the rows it holds, walking the containers
/// where the store reads its closure, so a test that registers a record sees it the way
/// the store would show it.
/// </remarks>
internal sealed class ResourcesInMemory : IResourceStore
{
    private readonly Dictionary<ResourceReference, RegisteredResource> _resources = [];

    /// <inheritdoc/>
    public ValueTask<RegisteredResource?> FindAsync(
        ResourceReference reference,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(_resources.GetValueOrDefault(reference));

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<RegisteredResource>> FindManyAsync(
        ResourceType type,
        IReadOnlyList<ResourceId> resources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        return ValueTask.FromResult<IReadOnlyList<RegisteredResource>>(
        [
            .. resources
                .Select(id => _resources.GetValueOrDefault(new ResourceReference(type, id)))
                .OfType<RegisteredResource>(),
        ]);
    }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(RegisteredResource resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);

        _resources[resource.Reference] = resource;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask RegisterManyAsync(
        IReadOnlyList<RegisteredResource> resources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        foreach (RegisteredResource resource in resources)
        {
            await RegisterAsync(resource, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask MoveAsync(
        ResourceReference reference,
        ResourceReference? containedIn,
        CancellationToken cancellationToken)
    {
        if (!_resources.TryGetValue(reference, out RegisteredResource? resource))
        {
            throw new InvalidOperationException("Only a registered record is moved.");
        }

        resource.MoveTo(containedIn);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ResourceId>> BeneathAsync(
        ResourceReference container,
        ResourceType type,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ResourceId>>(
        [
            .. _resources.Values
                .Where(resource => resource.Reference.Type == type
                    && Ancestry(resource.Reference).Contains(container))
                .Select(resource => resource.Reference.Id),
        ]);

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ResourceReference>> AncestryAsync(
        ResourceReference reference,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<ResourceReference>>(Ancestry(reference));

    // The record itself and then its containers, nearest first, as the closure is read.
    private List<ResourceReference> Ancestry(ResourceReference reference)
    {
        var ancestry = new List<ResourceReference>();

        for (ResourceReference? held = _resources.ContainsKey(reference) ? reference : null;
            held is ResourceReference step && !ancestry.Contains(step);
            held = _resources.GetValueOrDefault(step)?.ContainedIn)
        {
            ancestry.Add(step);
        }

        return ancestry;
    }
}
