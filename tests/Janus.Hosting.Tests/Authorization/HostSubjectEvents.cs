using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's own handler for the events the library raises about a person,
/// registered because the host declares its documents sensitive (PRIV-RIGHT-005b).
/// </summary>
public sealed class HostSubjectEvents : ISubjectEventSubscriber
{
    /// <inheritdoc/>
    public string Name => "host";

    /// <inheritdoc/>
    public bool Required => true;

    /// <inheritdoc/>
    public IReadOnlyCollection<ResourceType> Covers { get; } = [ResourceType.Parse("document")];

    /// <inheritdoc/>
    public ValueTask<Result> HandleAsync(SubjectEvent raised, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());
}
