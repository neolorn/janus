using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's own handler for the consent and objection changes of the purposes it
/// declares on consent, registered because the host declares its recommendations on
/// one (PRIV-RIGHT-001a, PRIV-CONS-008).
/// </summary>
public sealed class HostPurposes : IPurposeHandler
{
    /// <inheritdoc/>
    public string Name => "host-purposes";

    /// <inheritdoc/>
    public IReadOnlyCollection<string> Purposes { get; } = ["recommendations"];

    /// <inheritdoc/>
    public ValueTask<Result> ConsentChangedAsync(ConsentChanged changed, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());

    /// <inheritdoc/>
    public ValueTask<Result> ObjectionChangedAsync(ObjectionChanged changed, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success());
}
