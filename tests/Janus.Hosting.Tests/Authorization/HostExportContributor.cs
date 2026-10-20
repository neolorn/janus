using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Janus.Core;

namespace Janus.Hosting.Tests.Authorization;

/// <summary>
/// The host's own export contributor, registered because the host declares its documents
/// sensitive and holds a field of each under its owner's key (PRIV-RIGHT-003).
/// </summary>
public sealed class HostExportContributor : IExportContributor
{
    /// <inheritdoc/>
    public string Name => "host";

    /// <inheritdoc/>
    public IReadOnlyCollection<ResourceType> Covers { get; } = [ResourceType.Parse("document")];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ExportSection>>> SectionsAsync(
        SubjectId subject,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success<IReadOnlyList<ExportSection>>([]));
}
